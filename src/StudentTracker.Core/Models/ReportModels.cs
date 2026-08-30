namespace StudentTracker.Core.Models;

public class AwaitingOrderReportItem
{
    public string StudentName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime? OutcomeDate { get; set; }
    public decimal? CertificateCost { get; set; }
    public string CertificateOrderStatus { get; set; } = string.Empty;
    public string CashCommitmentStatus { get; set; } = string.Empty;
}

public class DeliveryReportItem
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Location { get; set; }
    public string? TrainerName { get; set; }
    public int? Capacity { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public int EnrolledCount { get; set; }
    public int? AvailablePlaces => Capacity.HasValue ? Capacity.Value - EnrolledCount : null;
}

public class AllocationReportItem
{
    public string StudentOrPlaceholder { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string AllocationStatus { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public string OutcomeStatus { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }
    public string? PlaceholderName { get; set; }
    public bool IsPlaceholder => !string.IsNullOrEmpty(PlaceholderName);
}

public class CourseUtilizationReportItem
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalAllocations { get; set; }
    public int Active { get; set; }
    public int Completed { get; set; }
    public int Withdrawn { get; set; }
    public int NotCompleted { get; set; }
    public int Cancelled { get; set; }
    public int Transferred { get; set; }
    public int Placeholders { get; set; }
    public decimal TotalCertificateCost { get; set; }
    public decimal TotalBudgetSpent { get; set; }
}

public class BudgetTransactionHistoryItem
{
    public string PoolName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? FundingSource { get; set; }
    public string? Reason { get; set; }
    public string? AllocationDisplayId { get; set; }
}

public class BudgetTransactionSummaryItem
{
    public string PoolName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class CreditTransactionHistoryItem
{
    public string PoolName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDateTime { get; set; }
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string? Reason { get; set; }
    public bool IsReconciled { get; set; }
}

public class CreditTransactionSummaryItem
{
    public string PoolName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalQuantity { get; set; }
}

public class CertificateOrderReportItem
{
    public string? StudentName { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public DateTime? OrderedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsReplacement { get; set; }
    public string? ReplacementReason { get; set; }
    public double? TurnaroundDays { get; set; }
    public string? ExternalReference { get; set; }
}

public class AuditLogReportItem
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityDisplayId { get; set; }
    public string? Reason { get; set; }
}

public class ImportReviewQueueReportItem
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSheet { get; set; } = string.Empty;
    public int SourceRow { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string ProposedAction { get; set; } = string.Empty;
    public string? Issue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
}

public class PrepaidPositionReportItem
{
    public string PoolDisplayId { get; set; } = string.Empty;
    public string PoolName { get; set; } = string.Empty;
    public string? FinancialPeriod { get; set; }
    public string? DeliveryDisplayId { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public decimal FundsAdded { get; set; }
    public decimal Committed { get; set; }
    public decimal Spent { get; set; }
    public decimal Available { get; set; }
    public int ReservedPlaces { get; set; }
    public int AssignedPending { get; set; }
    public int CompletedAwaitingSpend { get; set; }
    public int CompletionsRemaining { get; set; }
    public int TotalAllocations { get; set; }
    public int BillableUnexported { get; set; }
    public decimal? AllenCost { get; set; }
}

public class BillableCertificateReportItem
{
    public string? AllocationDisplayId { get; set; }
    public string? StudentName { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public DateTime? OutcomeDate { get; set; }
    public decimal? CertificateCost { get; set; }
    public bool IsExported { get; set; }
    public string? ExportBatchId { get; set; }
}

public class TbcDeliveryReportItem
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? TrainerName { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
}
