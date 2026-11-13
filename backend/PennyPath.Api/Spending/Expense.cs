namespace PennyPath.Api.Spending;

public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OwnerId { get; set; }
    public required string Name { get; set; }
    public required decimal Amount { get; set; }
    public required DateOnly PurchaseDate { get; set; }
    public required DateTime RecordedAtUtc { get; set; }
    public required string CategoryId { get; set; }
    public string? SubcategoryId { get; set; }
}
