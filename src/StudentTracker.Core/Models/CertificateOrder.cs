using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class CertificateOrder : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid AllocationId { get; set; }
    public Allocation? Allocation { get; set; }
    public Guid? OrderBatchId { get; set; }
    public DateTime? OrderedDate { get; set; }
    public string? Provider { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? CreditTransactionId { get; set; }
    public CertificateCreditTransaction? CreditTransaction { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public CertificateOrderStatus Status { get; set; } = CertificateOrderStatus.Ordered;
    public bool IsReplacement { get; set; }
    public string? ReplacementReason { get; set; }
}
