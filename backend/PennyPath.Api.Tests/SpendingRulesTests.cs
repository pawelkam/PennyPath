using System.Globalization;
using PennyPath.Api.Spending;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Spending")]
public class SpendingRulesTests
{
    [Theory]
    [InlineData("12", "12.00")]
    [InlineData("12.3", "12.30")]
    [InlineData("12.349", "12.34")]
    [InlineData("12.999", "12.99")]
    [InlineData("0.019", "0.01")]
    [InlineData("999999999999.999", "999999999999.99")]
    [InlineData("000000000001.001", "1.00")]
    public void AmountIsTruncatedBeforeConversion(string input, string expected)
    {
        Assert.True(ExpenseAmount.TryParse(input, out var amount));
        Assert.Equal(expected, ExpenseAmount.Format(amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0.009")]
    [InlineData("-0.01")]
    [InlineData("-12.34")]
    [InlineData("+12.34")]
    [InlineData("1,23")]
    [InlineData("1e2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData(" 12.34")]
    [InlineData("12.34 ")]
    [InlineData("1 000.00")]
    [InlineData("12 PLN")]
    [InlineData(".12")]
    [InlineData("12.")]
    [InlineData("1.2.3")]
    [InlineData("１２.３４")]
    [InlineData("1000000000000.00")]
    public void InvalidAmountIsRejected(string? input) => Assert.False(ExpenseAmount.TryParse(input, out _));

    [Fact]
    public void FractionLengthIsBoundedWithoutImplicitDecimalRounding()
    {
        Assert.True(ExpenseAmount.TryParse("1." + new string('9', 62), out var amount));
        Assert.Equal("1.99", ExpenseAmount.Format(amount));
        Assert.False(ExpenseAmount.TryParse("1." + new string('9', 63), out _));
        Assert.Equal("0.30", ExpenseAmount.Format(0.10m + 0.20m));
    }

    [Theory]
    [InlineData("2026-11-01T00:30:00Z", "Europe/Warsaw", "2026-11-01", "2026-11-01", "2026-12-01")]
    [InlineData("2026-11-01T00:30:00Z", "America/Los_Angeles", "2026-10-31", "2026-10-01", "2026-11-01")]
    [InlineData("2026-01-01T00:30:00Z", "America/Los_Angeles", "2025-12-31", "2025-12-01", "2026-01-01")]
    [InlineData("2024-02-29T12:00:00Z", "UTC", "2024-02-29", "2024-02-01", "2024-03-01")]
    [InlineData("2026-03-29T00:59:59Z", "Europe/Warsaw", "2026-03-29", "2026-03-01", "2026-04-01")]
    [InlineData("2026-03-29T01:00:00Z", "Europe/Warsaw", "2026-03-29", "2026-03-01", "2026-04-01")]
    [InlineData("2026-10-25T00:59:59Z", "Europe/Warsaw", "2026-10-25", "2026-10-01", "2026-11-01")]
    [InlineData("2026-10-25T01:00:00Z", "Europe/Warsaw", "2026-10-25", "2026-10-01", "2026-11-01")]
    public void AccountTimeZoneDeterminesTodayAndExclusiveMonth(string instant, string zone,
        string todayText, string start, string end)
    {
        var clock = new TestTimeProvider();
        clock.SetUtcNow(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));
        var today = SpendingPeriod.Today(clock, zone);
        Assert.Equal(DateOnly.Parse(todayText, CultureInfo.InvariantCulture), today);
        Assert.Equal(new SpendingPeriod(DateOnly.Parse(start, CultureInfo.InvariantCulture),
            DateOnly.Parse(end, CultureInfo.InvariantCulture)), SpendingPeriod.ForMonth(today));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Europe/Warsaw")]
    [InlineData("America/Los_Angeles")]
    [InlineData("US/Pacific")]
    public void SupportedIanaTimeZonesAndBrowserAliasesAreAccepted(string id) =>
        Assert.True(SpendingPeriod.TryFindTimeZone(id, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Wrong/Zone")]
    [InlineData(" Europe/Warsaw")]
    public void InvalidTimeZoneDoesNotFallBackToUtc(string? id) =>
        Assert.False(SpendingPeriod.TryFindTimeZone(id, out _));

    [Fact]
    public void SelectionIncludesUtcAndEveryOptionCanBeResolved()
    {
        var ids = SpendingPeriod.TimeZoneIds();
        Assert.Contains("UTC", ids);
        Assert.Contains("Europe/Warsaw", ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(SpendingPeriod.TryFindTimeZone(id, out _)));
    }
}
