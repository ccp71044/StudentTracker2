using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class FundingSource : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public FundingSourceType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateTime? DateReceived { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
