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
        string? DecisionNote,
        IReadOnlyList<ApprovalEntry> ApprovalHistory);

    private static InvoiceSnapshot CaptureSnapshot(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CustomerName,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.DecisionNote,
            invoice.ApprovalHistory.ToList());

    private static void AssertUnchanged(InvoiceSnapshot before, Invoice invoice, int beforeUpdateCount, int afterUpdateCount)
    {
        var after = CaptureSnapshot(invoice);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.CustomerName, after.CustomerName);
        Assert.Equal(before.Amount, after.Amount);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.DecidedBy, after.DecidedBy);
        Assert.Equal(before.DecidedAt, after.DecidedAt);
        Assert.Equal(before.DecisionNote, after.DecisionNote);
        Assert.Equal(before.ApprovalHistory, after.ApprovalHistory);
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
    public void Approve_HighValueInvoice_UsesTwoStageApprovalFlow()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var service = ServiceFor(invoice);

        var firstStage = service.Approve("INV-2", "demo.manager", "Manager");
        var firstStageSnapshot = CaptureSnapshot(firstStage);
        var firstHistoryEntry = Assert.Single(firstStageSnapshot.ApprovalHistory);

        Assert.Equal(InvoiceStatus.PendingApproval, firstStageSnapshot.Status);
        Assert.Equal("demo.manager", firstHistoryEntry.Username);
        Assert.Equal("Manager", firstHistoryEntry.Role);
        Assert.False(firstHistoryEntry.IsFinal);
        Assert.NotEqual(default, firstHistoryEntry.RecordedAt);

        var finalStage = service.Approve("INV-2", "demo.director", "Director");
        var secondHistoryEntry = finalStage.ApprovalHistory[1];

        Assert.Equal(InvoiceStatus.Approved, finalStage.Status);
        Assert.Equal("demo.director", finalStage.DecidedBy);
        Assert.Equal("demo.director", secondHistoryEntry.Username);
        Assert.Equal("Director", secondHistoryEntry.Role);
        Assert.True(secondHistoryEntry.IsFinal);
        Assert.NotEqual(default, secondHistoryEntry.RecordedAt);
        Assert.Equal(2, finalStage.ApprovalHistory.Count);
        Assert.False(finalStage.ApprovalHistory[0].IsFinal);
        Assert.Equal(firstStageSnapshot.ApprovalHistory[0].Username, finalStage.ApprovalHistory[0].Username);
        Assert.Equal(firstStageSnapshot.ApprovalHistory[0].Role, finalStage.ApprovalHistory[0].Role);
        Assert.Equal(firstStageSnapshot.ApprovalHistory[0].RecordedAt, finalStage.ApprovalHistory[0].RecordedAt);
    }

    [Fact]
    public void Approve_HighValueInvoice_PersistsFirstStageApproval()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        var result = service.Approve("INV-2", "demo.manager", "Manager");

        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Single(result.ApprovalHistory);
        Assert.False(result.ApprovalHistory[0].IsFinal);
    }

    [Fact]
    public void Approve_HighValueInvoice_PersistsFinalStageApproval()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        service.Approve("INV-2", "demo.manager", "Manager");
        var beforeUpdateCount = repository.UpdateCallCount;

        var result = service.Approve("INV-2", "demo.director", "Director");

        Assert.Equal(beforeUpdateCount + 1, repository.UpdateCallCount);
        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(2, result.ApprovalHistory.Count);
        Assert.True(result.ApprovalHistory[1].IsFinal);
    }

    [Fact]
    public void Approve_HighValueInvoice_RejectsRepeatedUsernameOnSecondStage()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        service.Approve("INV-2", "demo.manager", "Manager");
        var before = CaptureSnapshot(invoice);
        var beforeUpdateCount = repository.UpdateCallCount;

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-2", "demo.manager", "Director"));

        Assert.Contains("distinct", ex.Message, StringComparison.OrdinalIgnoreCase);
        AssertUnchanged(before, repository.GetById("INV-2")!, beforeUpdateCount, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_HighValueInvoice_RejectsNonDirectorSecondApprover()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        service.Approve("INV-2", "demo.director", "Director");
        var before = CaptureSnapshot(invoice);
        var beforeUpdateCount = repository.UpdateCallCount;

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-2", "demo.manager", "Manager"));

        Assert.Contains("Director", ex.Message);
        AssertUnchanged(before, repository.GetById("INV-2")!, beforeUpdateCount, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_HighValueInvoice_RejectsRepeatedUsernameWithoutMutation()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        service.Approve("INV-2", "demo.manager", "Manager");
        var before = CaptureSnapshot(invoice);
        var beforeUpdateCount = repository.UpdateCallCount;

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-2", "demo.manager", "Director"));

        Assert.Contains("distinct", ex.Message, StringComparison.OrdinalIgnoreCase);
        AssertUnchanged(before, repository.GetById("INV-2")!, beforeUpdateCount, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_HighValueInvoice_RejectsNonDirectorSecondApproverWithoutMutation()
    {
        var invoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 600000m);
        var (service, repository) = ServiceWithTracking(invoice);

        service.Approve("INV-2", "demo.director", "Director");
        var before = CaptureSnapshot(invoice);
        var beforeUpdateCount = repository.UpdateCallCount;

        var ex = Assert.Throws<ArgumentException>(() => service.Approve("INV-2", "demo.manager", "Manager"));

        Assert.Contains("Director", ex.Message);
        AssertUnchanged(before, repository.GetById("INV-2")!, beforeUpdateCount, repository.UpdateCallCount);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }
}
