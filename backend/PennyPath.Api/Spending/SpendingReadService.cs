using System.Data;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Data;

namespace PennyPath.Api.Spending;

public sealed class SpendingReadService(PennyPathIdentityDbContext database, TimeProvider clock)
{
    public async Task<SpendingOverviewDto?> ReadAsync(string ownerId, SpendingOverviewQuery query,
        CancellationToken cancellationToken, SpendingPeriod? explicitPeriod = null)
    {
        // PostgreSQL's default READ COMMITTED would give each SELECT a new snapshot.
        // SQLite supports SERIALIZABLE instead and remains a test-only provider.
        var isolation = database.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable;
        await using var transaction = await database.Database.BeginTransactionAsync(isolation, cancellationToken);
        var profile = await database.SpendingProfiles.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerId == ownerId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var today = SpendingPeriod.Today(clock, profile.TimeZoneId);
        var period = explicitPeriod ?? SpendingPeriod.ForMonth(today);
        var categories = await database.ExpenseCategories.AsNoTracking().Where(value => value.OwnerId == ownerId)
            .OrderBy(value => value.DisplayOrder).ThenBy(value => value.CategoryId).ToListAsync(cancellationToken);
        var subcategories = await database.ExpenseSubcategories.AsNoTracking().Where(value => value.OwnerId == ownerId)
            .OrderBy(value => value.DisplayOrder).ThenBy(value => value.SubcategoryId).ToListAsync(cancellationToken);
        var owned = database.Expenses.AsNoTracking().Where(value => value.OwnerId == ownerId);
        var monthly = owned.Where(value => value.PurchaseDate >= period.StartInclusive && value.PurchaseDate < period.EndExclusive);
        var monthlyRows = await monthly.ToListAsync(cancellationToken);
        var registerQuery = query.Scope == "all" ? owned : monthly;
        var count = await registerQuery.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        // A valid but distant page is empty, including offsets beyond Int32 SQL Skip.
        List<Expense> rows = offset >= count ? [] : await registerQuery.OrderByDescending(value => value.PurchaseDate)
            .ThenByDescending(value => value.RecordedAtUtc).ThenByDescending(value => value.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken);
        var names = categories.ToDictionary(value => value.CategoryId);
        var childNames = subcategories.ToDictionary(value => (value.CategoryId, value.SubcategoryId));
        var items = rows.Select(value => ToDto(value, names[value.CategoryId].Name,
            value.SubcategoryId is null ? null : childNames[(value.CategoryId, value.SubcategoryId)].Name)).ToArray();
        var summary = categories.Where(category => monthlyRows.Any(value => value.CategoryId == category.CategoryId))
            .Select(category =>
            {
                var categoryRows = monthlyRows.Where(value => value.CategoryId == category.CategoryId).ToArray();
                var children = new List<SubcategorySummaryDto>();
                var uncategorized = categoryRows.Where(value => value.SubcategoryId is null).ToArray();
                if (uncategorized.Length > 0)
                {
                    children.Add(new SubcategorySummaryDto(null, "Bez podkategorii",
                        ExpenseAmount.Format(uncategorized.Sum(value => value.Amount))));
                }

                foreach (var child in subcategories.Where(value => value.CategoryId == category.CategoryId))
                {
                    var childRows = categoryRows.Where(value => value.SubcategoryId == child.SubcategoryId).ToArray();
                    if (childRows.Length > 0)
                    {
                        children.Add(new SubcategorySummaryDto(child.SubcategoryId, child.Name,
                            ExpenseAmount.Format(childRows.Sum(value => value.Amount))));
                    }
                }

                return new CategorySummaryDto(category.CategoryId, category.Name,
                    ExpenseAmount.Format(categoryRows.Sum(value => value.Amount)), children);
            }).ToArray();
        var result = new SpendingOverviewDto(profile.TimeZoneId, today, period,
            new ExpenseRegisterDto(query.Scope, items, query.Page, query.PageSize, count),
            new SpendingSummaryDto(monthlyRows.Count, ExpenseAmount.Format(monthlyRows.Sum(value => value.Amount)), summary));
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public static ExpenseDto ToDto(Expense expense, string categoryName, string? subcategoryName) =>
        new(expense.Id, expense.Name, ExpenseAmount.Format(expense.Amount), expense.PurchaseDate,
            expense.CategoryId, categoryName, expense.SubcategoryId, subcategoryName,
            DateTime.SpecifyKind(expense.RecordedAtUtc, DateTimeKind.Utc));
}
