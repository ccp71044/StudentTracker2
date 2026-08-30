using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class ClientPrepaidPool : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Client { get; set; }
    public string? Description { get; set; }
    public string? FinancialPeriod { get; set; }

    // Restriction to a specific course, category, or unrestricted.
    public Guid? RestrictedToCourseDefinitionId { get; set; }
    public CourseDefinition? RestrictedToCourseDefinition { get; set; }
    public string? RestrictedToCourseCategory { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
