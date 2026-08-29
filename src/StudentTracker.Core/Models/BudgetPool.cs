using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class BudgetPool : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FinancialPeriod { get; set; }
    public BudgetPoolCategory Category { get; set; } = BudgetPoolCategory.Other;
    public string? ClientName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
