namespace PennyPath.Api.Spending;

public sealed class ExpenseSubcategory
{
    public required string OwnerId { get; set; }
    public required string CategoryId { get; set; }
    public required string SubcategoryId { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
}
