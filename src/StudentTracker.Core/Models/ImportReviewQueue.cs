using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class ImportReviewQueue : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSheet { get; set; } = string.Empty;
    public int SourceRow { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string ProposedAction { get; set; } = string.Empty;
    public string? ProposedValuesJson { get; set; }
    public string? Issue { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
