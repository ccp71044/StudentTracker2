using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class DocumentLink : EntityBase
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? LinkPurpose { get; set; }
}
