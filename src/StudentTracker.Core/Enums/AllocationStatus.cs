using System.ComponentModel;

namespace StudentTracker.Core.Enums;

public enum AllocationStatus
{
    [Description("A provisional place held; no budget or credit committed.")]
    Reserved,
    [Description("Student is formally enrolled and may consume budget/credit.")]
    Enrolled,
    [Description("Student is actively attending the delivery.")]
    Active,
    [Description("Student moved to another delivery.")]
    Transferred,
    [Description("Student left before completion; any commitments should be released.")]
    Withdrawn,
    [Description("Training completed and outcomes recorded.")]
    Finalised,
    [Description("Allocation cancelled; no budget or credit impact.")]
    Cancelled
}
