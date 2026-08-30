using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class ClientPrepaidEntitlementTransaction : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid PoolId { get; set; }
    public ClientPrepaidPool? Pool { get; set; }

    public Guid? AllocationId { get; set; }
    public Allocation? Allocation { get; set; }

    public Guid? LinkedTransactionId { get; set; }
    public ClientPrepaidEntitlementTransaction? LinkedTransaction { get; set; }

    public ClientPrepaidEntitlementTransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    // Quantity-based ledger. Positive for additions; negative for consumption/release.
    public decimal Quantity { get; set; }

    // Optional monetary reference value, e.g. the agreed per-place or total value.
    public decimal? MonetaryReferenceValue { get; set; }

    // Source invoice / reference that funded this prepaid place.
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
