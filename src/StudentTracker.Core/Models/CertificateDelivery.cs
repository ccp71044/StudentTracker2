using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class CertificateDelivery : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid CertificateOrderId { get; set; }
    public CertificateOrder? CertificateOrder { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? DeliveredTo { get; set; }
    public string? RecipientDetails { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public string? Notes { get; set; }
}
