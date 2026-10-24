namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, decided as a whole by a single approver.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver, string role)
    {
        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        if (invoice.Amount > 500000m)
        {
            return ApproveHighValue(invoice, approver, role);
        }

        EnsureRoleAndLimit(invoice, role);

        var approvedAt = DateTime.UtcNow;
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = approvedAt;
        invoice.ApprovalHistory.Add(new ApprovalEntry
        {
            Username = approver,
            Role = role,
            IsFinal = true,
            RecordedAt = approvedAt
        });
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void EnsureRoleAndLimit(Invoice invoice, string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Approval role is required.", nameof(role));
        }

        if (role == "Manager")
        {
            if (invoice.Amount > 50000m)
            {
                throw new ArgumentException(
                    $"Manager approval is limited to invoices up to 50000. Invoice {invoice.Id} amount {invoice.Amount} exceeds the limit.");
            }

            return;
        }

        if (role == "Director")
        {
            if (invoice.Amount > 500000m)
            {
                throw new ArgumentException(
                    $"Director approval is limited to invoices up to 500000. Invoice {invoice.Id} amount {invoice.Amount} exceeds the limit.");
            }

            return;
        }

        throw new ArgumentException("Only Manager and Director roles may approve invoices.", nameof(role));
    }

    private Invoice ApproveHighValue(Invoice invoice, string approver, string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Approval role is required.", nameof(role));
        }

        if (role != "Manager" && role != "Director")
        {
            throw new ArgumentException("Only Manager and Director roles may approve invoices.", nameof(role));
        }

        var existingApproval = invoice.ApprovalHistory.LastOrDefault();

        if (existingApproval is null)
        {
            var recordedAt = DateTime.UtcNow;
            invoice.ApprovalHistory.Add(new ApprovalEntry
            {
                Username = approver,
                Role = role,
                IsFinal = false,
                RecordedAt = recordedAt
            });
            _repository.Update(invoice);
            return invoice;
        }

        if (string.Equals(existingApproval.Username, approver, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "High-value invoice approvals require distinct approver usernames for each approval stage.", nameof(approver));
        }

        if (!string.Equals(role, "Director", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "For invoices above 500000, the second approver must be a Director.", nameof(role));
        }

        var finalAt = DateTime.UtcNow;
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = finalAt;
        invoice.ApprovalHistory.Add(new ApprovalEntry
        {
            Username = approver,
            Role = role,
            IsFinal = true,
            RecordedAt = finalAt
        });
        _repository.Update(invoice);
        return invoice;
    }
}
