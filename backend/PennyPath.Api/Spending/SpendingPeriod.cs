namespace PennyPath.Api.Spending;

public sealed record SpendingPeriod(DateOnly StartInclusive, DateOnly EndExclusive)
{
    public static DateOnly Today(TimeProvider clock, string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);

    public static SpendingPeriod ForMonth(DateOnly today)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        return new SpendingPeriod(start, start.AddMonths(1));
    }

    public static bool TryFindTimeZone(string? id, out TimeZoneInfo? timeZone)
    {
        timeZone = null;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return id == "UTC" || timeZone.HasIanaId;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    public static string[] TimeZoneIds() => TimeZoneInfo.GetSystemTimeZones()
        .Where(zone => zone.HasIanaId).Select(zone => zone.Id).Append("UTC")
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
