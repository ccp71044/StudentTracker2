using System.ComponentModel;

namespace StudentTracker.Core.Enums;

public enum BudgetPoolCategory
{
    [Description("Personal / internal funds")]
    Personal,
    [Description("Client-funded for a specific account")]
    ClientFunded,
    [Description("Other or uncategorised")]
    Other
}
