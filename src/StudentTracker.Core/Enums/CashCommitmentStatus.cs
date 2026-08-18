using System.ComponentModel;

namespace StudentTracker.Core.Enums;

public enum CashCommitmentStatus
{
    [Description("No budget impact")]
    None,
    [Description("Cash is reserved against the budget but not yet spent.")]
    Pending,
    [Description("Reserved cash has been released and is available again.")]
    Released,
    [Description("Cash has been spent and the budget has been reduced.")]
    Spent,
    [Description("A manual review of the budget status is required.")]
    ReviewRequired
}
