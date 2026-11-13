using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PennyPath.Api.Data;
using PennyPath.Api.Spending;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Spending")]
public class SpendingReconciliationTests
{
    [Theory]
    [InlineData("empty-register")]
    [InlineData("exact-cents")]
    [InlineData("category-hierarchy")]
    [InlineData("purchase-date-boundaries")]
    [InlineData("calendar-year-boundary")]
    [InlineData("distinct-register-rows")]
    public async Task OriginalFixtureMatchesPersistedRegisterAndProductionSummary(string caseId)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "spending-reconciliation", "cases.json"), Cancellation));
        var fixture = document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => value.GetProperty("id").GetString() == caseId);
        var expenses = fixture.GetProperty("expenses").EnumerateArray().ToArray();
        var ids = expenses.ToDictionary(value => Text(value, "id"), _ => Guid.NewGuid());
        await using var factory = new TestApplicationFactory();
        await factory.InitializeDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
            database.Users.Add(new IdentityUser("fixture") { Id = "fixture" });
            database.SpendingProfiles.Add(new SpendingProfile { OwnerId = "fixture", TimeZoneId = "UTC" });
            foreach (var category in expenses.Select(value => Text(value, "category_id")).Distinct())
            {
                database.ExpenseCategories.Add(new ExpenseCategory
                {
                    OwnerId = "fixture", CategoryId = category, Name = category
                });
            }

            foreach (var pair in expenses.Select(value => (Category: Text(value, "category_id"), Child: Text(value, "subcategory_id"))).Distinct())
            {
                database.ExpenseSubcategories.Add(new ExpenseSubcategory
                {
                    OwnerId = "fixture", CategoryId = pair.Category, SubcategoryId = pair.Child, Name = pair.Child
                });
            }

            foreach (var row in expenses)
            {
                database.Expenses.Add(new Expense
                {
                    Id = ids[Text(row, "id")], OwnerId = "fixture", Name = Text(row, "id"),
                    Amount = decimal.Parse(Text(row, "amount"), CultureInfo.InvariantCulture),
                    PurchaseDate = DateOnly.ParseExact(Text(row, "purchase_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    RecordedAtUtc = DateOnly.ParseExact(Text(row, "recorded_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture)
                        .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    CategoryId = Text(row, "category_id"), SubcategoryId = Text(row, "subcategory_id")
                });
            }

            await database.SaveChangesAsync(Cancellation);
        }

        // Reopen the relational store before invoking the same read path used by HTTP.
        await using var readScope = factory.Services.CreateAsyncScope();
        var period = fixture.GetProperty("period");
        var result = await readScope.ServiceProvider.GetRequiredService<SpendingReadService>().ReadAsync("fixture",
            new SpendingOverviewQuery(), Cancellation, new SpendingPeriod(
                DateOnly.Parse(Text(period, "start_inclusive"), CultureInfo.InvariantCulture),
                DateOnly.Parse(Text(period, "end_exclusive"), CultureInfo.InvariantCulture)));
        Assert.NotNull(result);
        var expected = fixture.GetProperty("expected");
        var expectedIds = expected.GetProperty("expense_ids").EnumerateArray().Select(value => ids[value.GetString()!]).Order().ToArray();
        Assert.Equal(expectedIds, result.Register.Items.Select(value => value.Id).Order().ToArray());
        Assert.Equal(expectedIds.Length, result.Register.TotalCount);
        Assert.Equal(expectedIds.Length, result.Summary.ExpenseCount);
        Assert.Equal(Text(expected, "total"), result.Summary.Total);
        var expectedCategories = expected.GetProperty("categories").EnumerateObject().ToDictionary(value => value.Name, value => value.Value.GetString());
        Assert.Equal(expectedCategories.Count, result.Summary.Categories.Count);
        foreach (var category in result.Summary.Categories)
        {
            Assert.Equal(expectedCategories[category.Id], category.Total);
            var children = expected.GetProperty("subcategories").GetProperty(category.Id)
                .EnumerateObject().ToDictionary(value => value.Name, value => value.Value.GetString());
            Assert.Equal(children.Count, category.Subcategories.Count);
            foreach (var child in category.Subcategories)
            {
                Assert.NotNull(child.Id);
                Assert.Equal(children[child.Id], child.Total);
            }

            Assert.Equal(decimal.Parse(category.Total, CultureInfo.InvariantCulture),
                category.Subcategories.Sum(value => decimal.Parse(value.Total, CultureInfo.InvariantCulture)));
        }

        Assert.Equal(decimal.Parse(result.Summary.Total, CultureInfo.InvariantCulture),
            result.Summary.Categories.Sum(value => decimal.Parse(value.Total, CultureInfo.InvariantCulture)));
    }

    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString()!;
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
}
