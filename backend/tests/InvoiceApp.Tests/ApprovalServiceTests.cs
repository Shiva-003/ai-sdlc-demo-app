using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    private static InvoiceSnapshot CaptureSnapshot(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CustomerName,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.DecisionNote);

    private static void AssertUnchanged(InvoiceSnapshot before, Invoice invoice, int beforeUpdateCount, int afterUpdateCount)
    {
        var after = CaptureSnapshot(invoice);

        Assert.Equal(before, after);
        Assert.Equal(beforeUpdateCount, afterUpdateCount);
    }

    private sealed class TrackingInvoiceRepository : IInvoiceRepository
    {
        private readonly List<Invoice> _invoices;

        public TrackingInvoiceRepository(params Invoice[] seed)
        {
            _invoices = new List<Invoice>(seed);
        }

        public int UpdateCallCount { get; private set; }

        public IReadOnlyList<Invoice> GetAll() => _invoices.ToList();
        public Invoice? GetById(string id) => _invoices.FirstOrDefault(i => i.Id == id);
        public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) => _invoices.Where(i => ids.Contains(i.Id)).ToList();
        public Invoice Add(Invoice invoice) { _invoices.Add(invoice); return invoice; }
        public void Update(Invoice invoice)
        {
            UpdateCallCount++;
            var index = _invoices.FindIndex(i => i.Id == invoice.Id);
            if (index >= 0)
            {
                _invoices[index] = invoice;
            }
            else
            {
                _invoices.Add(invoice);
            }
        }
        public bool Remove(string id) => _invoices.RemoveAll(i => i.Id == id) > 0;
    }

    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    private static (ApprovalService Service, TrackingInvoiceRepository Repository) ServiceWithTracking(params Invoice[] invoices)
    {
        var repository = new TrackingInvoiceRepository(invoices);
        return (new ApprovalService(repository), repository);
    }

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_Manager_AllowsExactThreshold()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50000m));

        var result = service.Approve("INV-1", "demo.manager", "Manager");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_Director_AllowsExactThreshold()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000m));

        var result = service.Approve("INV-1", "demo.director", "Director");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_Manager_RejectsAmountJustOverLimit()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 50000.01m);
        var (service, repository) = ServiceWithTracking(invoice);
        var before = CaptureSnapshot(invoice);

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "demo.manager", "Manager"));

        Assert.Contains("Manager", ex.Message);
        Assert.Contains("50000", ex.Message);
        AssertUnchanged(before, repository.GetById("INV-1")!, 0, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_Director_RejectsAmountJustOverLimit()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 500000.01m);
        var (service, repository) = ServiceWithTracking(invoice);
        var before = CaptureSnapshot(invoice);

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "demo.director", "Director"));

        Assert.Contains("Director", ex.Message);
        Assert.Contains("500000", ex.Message);
        AssertUnchanged(before, repository.GetById("INV-1")!, 0, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_RejectsDisallowedRole()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 25000m));

        Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "demo.approver", "Approver"));
        Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "demo.admin", "Admin"));
    }

    [Fact]
    public void Approve_RejectsInvalidRoleBeforeMutatingState()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval, 25000m);
        var (service, repository) = ServiceWithTracking(invoice);
        var before = CaptureSnapshot(invoice);

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "demo.admin", "Admin"));

        Assert.Contains("Manager", ex.Message);
        Assert.Contains("Director", ex.Message);
        AssertUnchanged(before, repository.GetById("INV-1")!, 0, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.manager", "Manager"));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver", "Manager"));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }
}
