using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class SignOffParticipant : EntityBase
{
    public Guid SignOffId { get; set; }
    public SignOff? SignOff { get; set; }
    public Guid? AllocationId { get; set; }
    public Allocation? Allocation { get; set; }
    public string? StudentDisplayName { get; set; }
    public string? DeliveryDateText { get; set; }
    public string? ParticipantNote { get; set; }
    public int SortOrder { get; set; }
    public bool Attended { get; set; }
    public string? OutcomeText { get; set; }
}
