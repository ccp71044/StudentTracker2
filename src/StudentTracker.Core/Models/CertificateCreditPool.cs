using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class CertificateCreditPool : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Description { get; set; }
    public CreditUnitType UnitType { get; set; } = CreditUnitType.Count;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public enum CreditUnitType
{
    Monetary,
    Count
}
