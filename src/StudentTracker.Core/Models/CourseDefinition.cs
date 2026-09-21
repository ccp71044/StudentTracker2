using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class CourseDefinition : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    /// <summary>Stable key used to match this course across the register, price list and provider ledger.</summary>
    public string? MatchKey { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public decimal? DefaultCertificateCost { get; set; }
    public decimal? DefaultAllensCost { get; set; }
    public int? CourseDurationDays { get; set; }
    public decimal? DefaultCreditQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
