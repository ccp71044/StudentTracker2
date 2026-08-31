using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class Allocation : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public Guid CourseDeliveryId { get; set; }
    public CourseDelivery? CourseDelivery { get; set; }
    public string? PlaceholderName { get; set; }
    public string? LegacyReference { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    public AllocationStatus AllocationStatus { get; set; } = AllocationStatus.Enrolled;
    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.NotRecorded;
    public OutcomeStatus OutcomeStatus { get; set; } = OutcomeStatus.Pending;
    public DateTime? OutcomeDate { get; set; }
    public Guid? OutcomeReasonId { get; set; }
    public OutcomeReason? OutcomeReason { get; set; }
    public string? OutcomeNotes { get; set; }
    public decimal? CertificateCost { get; set; }
    public decimal? AllensCostAtAllocation { get; set; }
    public decimal? ActualAllensCost { get; set; }
    public Guid? BudgetPoolId { get; set; }
    public BudgetPool? BudgetPool { get; set; }
    public Guid? CreditPoolId { get; set; }
    public CertificateCreditPool? CreditPool { get; set; }
    public Guid? ClientPrepaidPoolId { get; set; }
    public ClientPrepaidPool? ClientPrepaidPool { get; set; }
    public Guid? ClientPrepaidEntitlementTransactionId { get; set; }
    public ClientPrepaidEntitlementTransaction? ClientPrepaidEntitlementTransaction { get; set; }
    public CashCommitmentStatus CashCommitmentStatus { get; set; } = CashCommitmentStatus.None;
    public CreditStatus CreditStatus { get; set; } = CreditStatus.None;
    public CertificateOrderStatus CertificateOrderStatus { get; set; } = CertificateOrderStatus.NotReady;
    public CertificateDeliveryStatus CertificateDeliveryStatus { get; set; } = CertificateDeliveryStatus.NotApplicable;
    public bool IsBillable { get; set; }
    public DateTime? BillableDate { get; set; }
    public Guid? ExportedInBatchId { get; set; }
    public string? Notes { get; set; }
}
