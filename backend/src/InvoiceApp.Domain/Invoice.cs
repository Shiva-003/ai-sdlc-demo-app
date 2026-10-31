namespace InvoiceApp.Domain;

public sealed record ApprovalEntry
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public List<ApprovalEntry> ApprovalHistory { get; set; } = new();
}
