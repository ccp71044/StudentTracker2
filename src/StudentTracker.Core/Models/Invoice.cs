using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class Invoice : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string? ExternalInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Customer { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? GSTAmount { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? AmountAssignedToStudentTracker { get; set; }
    public Guid? FileDocumentId { get; set; }
    public Document? FileDocument { get; set; }
    public string? Notes { get; set; }
}
