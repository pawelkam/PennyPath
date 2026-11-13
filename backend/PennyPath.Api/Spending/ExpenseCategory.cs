namespace PennyPath.Api.Spending;

public sealed class ExpenseCategory
{
    public required string OwnerId { get; set; }
    public required string CategoryId { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
}
