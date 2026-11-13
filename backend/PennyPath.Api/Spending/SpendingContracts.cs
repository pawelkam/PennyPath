using System.ComponentModel.DataAnnotations;

namespace PennyPath.Api.Spending;

public sealed class SpendingSettingsRequest
{
    [Required, StringLength(128)]
    public string? TimeZoneId { get; init; }
}

public sealed class CreateExpenseRequest
{
    private string? name;

    [Required, StringLength(200)]
    public string? Name
    {
        get => name;
        init => name = value?.Trim();
    }

    [Required, StringLength(64)]
    public string? Amount { get; init; }

    [Required]
    public string? PurchaseDate { get; init; }

    [Required, StringLength(64)]
    public string? CategoryId { get; init; }

    [StringLength(64, MinimumLength = 1)]
    public string? SubcategoryId { get; init; }
}

public sealed class SpendingOverviewQuery
{
    public string Scope { get; init; } = "current";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record SpendingSettingsResponse(string? TimeZoneId);
public sealed record TimeZonesResponse(IReadOnlyList<string> TimeZoneIds);
public sealed record SavedSpendingSettingsResponse(string TimeZoneId, DateOnly Today, SpendingPeriod Period);
public sealed record ExpenseSubcategoryDto(string Id, string Name);
public sealed record ExpenseCategoryDto(string Id, string Name, IReadOnlyList<ExpenseSubcategoryDto> Subcategories);
public sealed record ExpenseCategoriesResponse(IReadOnlyList<ExpenseCategoryDto> Categories);
public sealed record ExpenseDto(Guid Id, string Name, string Amount, DateOnly PurchaseDate,
    string CategoryId, string CategoryName, string? SubcategoryId, string? SubcategoryName, DateTime RecordedAtUtc);
public sealed record ExpenseRegisterDto(string Scope, IReadOnlyList<ExpenseDto> Items, int Page,
    int PageSize, long TotalCount);
public sealed record SubcategorySummaryDto(string? Id, string Name, string Total);
public sealed record CategorySummaryDto(string Id, string Name, string Total,
    IReadOnlyList<SubcategorySummaryDto> Subcategories);
public sealed record SpendingSummaryDto(long ExpenseCount, string Total, IReadOnlyList<CategorySummaryDto> Categories);
public sealed record SpendingOverviewDto(string TimeZoneId, DateOnly Today, SpendingPeriod Period,
    ExpenseRegisterDto Register, SpendingSummaryDto Summary);
