using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class CertificateCreditTransaction : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid PoolId { get; set; }
    public CertificateCreditPool? Pool { get; set; }
    public Guid? AllocationId { get; set; }
    public Allocation? Allocation { get; set; }
    public Guid? LinkedTransactionId { get; set; }
    public CertificateCreditTransaction? LinkedTransaction { get; set; }
    public CreditTransactionType TransactionType { get; set; }
    public DateTime TransactionDateTime { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public CreditSourceType SourceType { get; set; } = CreditSourceType.Manual;
    public string? ExternalTransactionId { get; set; }
    public string? ExternalCourseNumber { get; set; }
    public string? ExternalPurchaseReference { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public bool IsReconciled { get; set; }

    /// <summary>
    /// Marks credit that was lost rather than spent on a certificate, for example a withdrawal
    /// with insufficient notice to reallocate the position. Reported as "unavailable".
    /// </summary>
    public bool IsCreditLoss { get; set; }
}

public enum CreditSourceType
{
    Manual,
    ProviderHistory,
    LegacyTopUp,
    Invoice,
    Reconciliation,
    Migration
}
