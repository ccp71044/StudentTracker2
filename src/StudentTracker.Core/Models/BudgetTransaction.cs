using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class BudgetTransaction : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid PoolId { get; set; }
    public BudgetPool? Pool { get; set; }
    public Guid? AllocationId { get; set; }
    public Allocation? Allocation { get; set; }
    public BudgetTransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public Guid? FundingSourceId { get; set; }
    public FundingSource? FundingSource { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
