using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class OutcomeReason : EntityBase
{
    public string ReasonType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool RequiresNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
