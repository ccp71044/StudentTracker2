using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class SignOff : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid CourseDeliveryId { get; set; }
    public CourseDelivery? CourseDelivery { get; set; }
    public int Version { get; set; } = 1;
    public SignOffStatus Status { get; set; } = SignOffStatus.Draft;
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LockedDate { get; set; }
    public Guid? FileDocumentId { get; set; }
    public Document? FileDocument { get; set; }
    public string? TrainerName { get; set; }
    public string? TrainerDetails { get; set; }
    public DateTime? TrainerSignedDate { get; set; }
    public string? AuthorisedByName { get; set; }
    public string? AuthorisedByPosition { get; set; }
    public DateTime? AuthorisedSignedDate { get; set; }
    public string? VerifiedByName { get; set; }
    public string? VerifiedByPosition { get; set; }
    public DateTime? VerifiedSignedDate { get; set; }
    public string? Notes { get; set; }

    public List<SignOffParticipant> Participants { get; set; } = new();
}
