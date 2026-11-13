using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Data;

namespace PennyPath.Api.Spending;

public sealed class SpendingSettingsService(PennyPathIdentityDbContext database, TimeProvider clock)
{
    public async Task<SavedSpendingSettingsResponse> SaveAsync(string ownerId, string timeZoneId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Both providers support this atomic claim. A competing first request waits for
        // the winner's transaction; only the inserted profile's request seeds the catalog.
        var inserted = await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SpendingProfiles" ("OwnerId", "TimeZoneId")
            VALUES ({ownerId}, {timeZoneId}) ON CONFLICT ("OwnerId") DO NOTHING
            """, cancellationToken);
        if (inserted == 1)
        {
            InitialExpenseCategories.AddTo(database, ownerId);
            await database.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await database.SpendingProfiles.Where(profile => profile.OwnerId == ownerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.TimeZoneId, timeZoneId), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var today = SpendingPeriod.Today(clock, timeZoneId);
        return new SavedSpendingSettingsResponse(timeZoneId, today, SpendingPeriod.ForMonth(today));
    }
}
