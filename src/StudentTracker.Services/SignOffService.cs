using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class SignOffService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public SignOffService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<SignOff> GenerateDraftAsync(Guid deliveryId, List<Guid> allocationIds, string trainerName, string? trainerDetails = null)
    {
        var delivery = await _context.CourseDeliveries
            .Include(d => d.CourseDefinition)
            .FirstAsync(d => d.Id == deliveryId);

        var allocations = await _context.Allocations
            .Where(a => allocationIds.Contains(a.Id))
            .Include(a => a.Student)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var settings = await _context.AppSettings.FirstAsync();
        var signOff = new SignOff
        {
            DisplayId = _idGenerator.NextDisplayId<SignOff>("SGN"),
            CourseDeliveryId = deliveryId,
            Version = await _context.SignOffs.Where(s => s.CourseDeliveryId == deliveryId).MaxAsync(s => (int?)s.Version) + 1 ?? 1,
            Status = SignOffStatus.Draft,
            GeneratedDate = DateTime.UtcNow,
            TrainerName = trainerName,
            TrainerDetails = trainerDetails ?? delivery.TrainerBusinessDetails,
            AuthorisedByName = settings.DefaultAuthorisedByName,
            AuthorisedByPosition = settings.DefaultAuthorisedByPosition,
            VerifiedByName = settings.DefaultVerifiedByName,
            VerifiedByPosition = settings.DefaultVerifiedByPosition,
            Notes = delivery.Notes
        };
        _context.SignOffs.Add(signOff);
        await _context.SaveChangesAsync();

        int order = 0;
        foreach (var a in allocations)
        {
            _context.SignOffParticipants.Add(new SignOffParticipant
            {
                SignOffId = signOff.Id,
                AllocationId = a.Id,
                StudentDisplayName = a.Student?.FullName ?? a.PlaceholderName ?? "Unknown",
                DeliveryDateText = FormatDeliveryDate(delivery, a),
                ParticipantNote = a.OutcomeNotes,
                SortOrder = order++,
                Attended = a.AttendanceStatus == AttendanceStatus.Attended || a.AttendanceStatus == AttendanceStatus.Confirmed,
                OutcomeText = a.OutcomeStatus.ToString()
            });
        }
        await _context.SaveChangesAsync();
        _audit.Record("Generated", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
        return signOff;
    }

    public async Task<SignOff?> GetAsync(Guid id) => await _context.SignOffs
        .Include(s => s.Participants)
        .Include(s => s.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
        .Include(s => s.FileDocument)
        .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<SignOff>> GetForDeliveryAsync(Guid deliveryId) => await _context.SignOffs
        .Where(s => s.CourseDeliveryId == deliveryId)
        .Include(s => s.Participants)
        .Include(s => s.FileDocument)
        .OrderByDescending(s => s.Version)
        .ToListAsync();

    public async Task LockAsync(Guid signOffId)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        if (signOff.Status == SignOffStatus.Signed)
            throw new InvalidOperationException("Sign-off is already locked.");
        signOff.Status = SignOffStatus.Signed;
        signOff.LockedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Locked", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSignedDatesAsync(Guid signOffId, DateTime? trainerSignedDate, DateTime? authorisedSignedDate, DateTime? verifiedSignedDate)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        if (signOff.Status == SignOffStatus.Signed)
            throw new InvalidOperationException("Cannot update a locked sign-off.");
        signOff.TrainerSignedDate = trainerSignedDate;
        signOff.AuthorisedSignedDate = authorisedSignedDate;
        signOff.VerifiedSignedDate = verifiedSignedDate;
        signOff.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("UpdatedSignedDates", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateDetailsAsync(Guid signOffId, string? trainerName, string? trainerDetails,
        string? authorisedByName, string? authorisedByPosition,
        string? verifiedByName, string? verifiedByPosition, string? notes)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        if (signOff.Status == SignOffStatus.Signed)
            throw new InvalidOperationException("Cannot update a locked sign-off.");
        signOff.TrainerName = trainerName;
        signOff.TrainerDetails = trainerDetails;
        signOff.AuthorisedByName = authorisedByName;
        signOff.AuthorisedByPosition = authorisedByPosition;
        signOff.VerifiedByName = verifiedByName;
        signOff.VerifiedByPosition = verifiedByPosition;
        signOff.Notes = notes;
        signOff.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("UpdatedDetails", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task SetStatusReadyForSignatureAsync(Guid signOffId)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        if (signOff.Status == SignOffStatus.Signed)
            throw new InvalidOperationException("Cannot change status of a locked sign-off.");
        signOff.Status = SignOffStatus.ReadyForSignature;
        signOff.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ReadyForSignature", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task SupersedeAsync(Guid signOffId)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        if (signOff.Status == SignOffStatus.Signed)
            throw new InvalidOperationException("Cannot supersede a locked sign-off.");
        signOff.Status = SignOffStatus.Superseded;
        signOff.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Superseded", "SignOff", signOff.Id, signOff.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task SetFileDocumentIdAsync(Guid signOffId, Guid documentId)
    {
        var signOff = await _context.SignOffs.FindAsync(signOffId) ?? throw new ArgumentException("Sign-off not found");
        signOff.FileDocumentId = documentId;
        signOff.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("LinkedDocument", "SignOff", signOff.Id, signOff.DisplayId, null, new { DocumentId = documentId });
        await _context.SaveChangesAsync();
    }

    private string FormatDeliveryDate(CourseDelivery delivery, Allocation allocation)
    {
        if (delivery.DateStatus == DeliveryDateStatus.TBC) return "TBC";
        if (delivery.DateStatus == DeliveryDateStatus.Blank) return "";
        if (delivery.StartDate == null) return "";
        if (delivery.EndDate == null || delivery.StartDate == delivery.EndDate)
            return delivery.StartDate.Value.ToString("dd/MM/yyyy");
        return $"{delivery.StartDate.Value:dd/MM/yyyy} - {delivery.EndDate.Value:dd/MM/yyyy}";
    }
}
