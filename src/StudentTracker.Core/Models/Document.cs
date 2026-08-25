using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class Document : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? MimeType { get; set; }
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public Guid? CategoryId { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;

    /// <summary>
    /// The document this one replaces. Superseded versions keep their file and links so the
    /// history of what was actually signed or received is never lost.
    /// </summary>
    public Guid? SupersedesDocumentId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public string? Confidentiality { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedDate { get; set; }
    public string? Notes { get; set; }

    public List<DocumentLink> Links { get; set; } = new();
}
