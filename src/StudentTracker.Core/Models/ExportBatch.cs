using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class ExportBatch : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public List<ExportBatchItem> Items { get; set; } = new();
}

public class ExportBatchItem : EntityBase
{
    public Guid ExportBatchId { get; set; }
    public ExportBatch? ExportBatch { get; set; }
    public Guid AllocationId { get; set; }
    public Allocation? Allocation { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
