using System.ComponentModel;

namespace StudentTracker.Core.Enums;

public enum CreditStatus
{
    [Description("No credit pool impact")]
    None,
    [Description("A credit has been reserved against a pool for this allocation.")]
    Allocated,
    [Description("Credit has been used and the pool balance reduced.")]
    Consumed,
    [Description("Reserved credit has been returned to the pool.")]
    Released,
    [Description("Credit has been moved to a different allocation.")]
    Reallocated,
    [Description("Credit is no longer valid for this period.")]
    Expired,
    [Description("Credit is not available for this allocation.")]
    Unavailable,
    [Description("A manual review of the credit status is required.")]
    ReviewRequired
}
