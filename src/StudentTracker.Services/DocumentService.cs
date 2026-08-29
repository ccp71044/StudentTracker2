using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public sealed record DocumentLinkTarget(Guid Id, string DisplayId, string Description)
{
    public string FriendlyName => string.IsNullOrWhiteSpace(Description) ? DisplayId : $"{DisplayId} — {Description}";
}

public class DocumentService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DataLocationService _dataLocation;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public DocumentService(StudentTrackerDbContext context, DataLocationService dataLocation, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _dataLocation = dataLocation;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<Document> AddDocumentAsync(string sourcePath, string categoryFolder, string? displayName = null, string? description = null, string? mimeType = null, DateTime? receivedDate = null)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source file not found", sourcePath);

        var ext = Path.GetExtension(sourcePath);
        var originalName = Path.GetFileName(sourcePath);
        var managedName = $"{Guid.NewGuid()}{ext}";
        var relative = Path.Combine(categoryFolder, managedName);
        var dest = Path.Combine(_dataLocation.DocumentsPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(sourcePath, dest, overwrite: true);

        var sha = await ComputeSha256Async(dest);
        var doc = new Document
        {
            DisplayId = _idGenerator.NextDisplayId<Document>("DOC"),
            OriginalFileName = originalName,
            StoredFileName = managedName,
            RelativePath = relative,
            Extension = ext,
            MimeType = mimeType ?? GetMimeType(ext),
            FileSize = new FileInfo(dest).Length,
            Sha256 = sha,
            DisplayName = displayName ?? originalName,
            Description = description,
            ReceivedDate = receivedDate,
            Status = DocumentStatus.Active
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "Document", doc.Id, doc.DisplayId);
        await _context.SaveChangesAsync();
        return doc;
    }

    public async Task<Document> UpdateMetadataAsync(Guid documentId, string displayName, string? description, DateTime? receivedDate, string? confidentiality, string? notes)
    {
        var document = await _context.Documents.FindAsync(documentId) ?? throw new ArgumentException("Document not found");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required", nameof(displayName));
        document.DisplayName = displayName.Trim();
        document.Description = description;
        document.ReceivedDate = receivedDate;
        document.Confidentiality = confidentiality;
        document.Notes = notes;
        document.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "Document", document.Id, document.DisplayId);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<List<DocumentLinkTarget>> GetLinkTargetsAsync(string entityType)
    {
        return entityType switch
        {
            "Student" => await _context.Students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new DocumentLinkTarget(s.Id, s.DisplayId ?? s.Id.ToString(), s.FirstName + " " + s.LastName)).ToListAsync(),
            "Allocation" => await _context.Allocations.OrderByDescending(a => a.AllocatedAt)
                .Select(a => new DocumentLinkTarget(a.Id, a.DisplayId ?? a.Id.ToString(), a.Student != null ? a.Student.FirstName + " " + a.Student.LastName : a.PlaceholderName ?? string.Empty)).ToListAsync(),
            "CourseDelivery" => await _context.CourseDeliveries.OrderByDescending(d => d.StartDate)
                .Select(d => new DocumentLinkTarget(d.Id, d.DisplayId ?? d.Id.ToString(), d.CourseDefinition != null ? d.CourseDefinition.CourseCode + " - " + d.CourseDefinition.CourseTitle : string.Empty)).ToListAsync(),
            "CertificateOrder" => await _context.CertificateOrders.OrderByDescending(o => o.OrderedDate)
                .Select(o => new DocumentLinkTarget(o.Id, o.DisplayId ?? o.Id.ToString(), o.Provider ?? string.Empty)).ToListAsync(),
            _ => throw new ArgumentException("Unsupported link type", nameof(entityType))
        };
    }

    public async Task<DocumentLink> LinkDocumentAsync(Guid documentId, string entityType, Guid entityId, string? purpose = null)
    {
        if (await _context.DocumentLinks.AnyAsync(l => l.DocumentId == documentId && l.EntityType == entityType && l.EntityId == entityId))
            throw new InvalidOperationException("The document is already linked to this record.");
        var link = new DocumentLink
        {
            DocumentId = documentId,
            EntityType = entityType,
            EntityId = entityId,
            LinkPurpose = purpose
        };
        _context.DocumentLinks.Add(link);
        await _context.SaveChangesAsync();
        _audit.Record("Linked", "DocumentLink", link.Id);
        await _context.SaveChangesAsync();
        return link;
    }

    public async Task<List<Document>> GetDocumentsForEntityAsync(string entityType, Guid entityId, bool includeArchived = false)
    {
        if (entityType == "All")
            return await _context.Documents.Where(d => includeArchived || d.Status != DocumentStatus.Archived).OrderBy(d => d.DisplayName).ToListAsync();

        return await _context.DocumentLinks
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .Include(l => l.Document)
            .Select(l => l.Document!)
            .Where(d => includeArchived || d.Status != DocumentStatus.Archived)
            .ToListAsync();
    }

    public async Task DeleteLinkAsync(Guid documentId, string entityType, Guid entityId)
    {
        var link = await _context.DocumentLinks
            .FirstOrDefaultAsync(l => l.DocumentId == documentId && l.EntityType == entityType && l.EntityId == entityId);
        if (link != null)
        {
            _context.DocumentLinks.Remove(link);
            await _context.SaveChangesAsync();
            _audit.Record("Unlinked", "DocumentLink", link.Id);
            await _context.SaveChangesAsync();
        }

        var otherLinks = await _context.DocumentLinks.AnyAsync(l => l.DocumentId == documentId);
        if (!otherLinks)
        {
            var doc = await _context.Documents.FindAsync(documentId);
            if (doc != null)
            {
                doc.Status = DocumentStatus.Archived;
                await _context.SaveChangesAsync();
            }
        }
    }

    public string GetFullPath(Document document) => Path.Combine(_dataLocation.DocumentsPath, document.RelativePath);

    public string GetDocumentPath(Guid documentId)
    {
        var doc = _context.Documents.Find(documentId);
        return doc != null ? GetFullPath(doc) : string.Empty;
    }

    public async Task ArchiveDocumentAsync(Guid documentId)
    {
        var doc = await _context.Documents.FindAsync(documentId) ?? throw new ArgumentException("Document not found");
        var evidenceLinks = await _context.CertificateDeliveries.CountAsync(d => d.EvidenceDocumentId == documentId);
        if (evidenceLinks > 0)
        {
            _audit.Record("ArchiveBlocked", "Document", doc.Id, doc.DisplayId, null, new { CertificateEvidenceLinks = evidenceLinks });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException($"Document is evidence for {evidenceLinks} certificate delivery record(s) and cannot be archived.");
        }
        doc.Status = DocumentStatus.Archived;
        doc.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Archived", "Document", documentId, doc.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task RestoreDocumentAsync(Guid documentId)
    {
        var doc = await _context.Documents.FindAsync(documentId) ?? throw new ArgumentException("Document not found");
        doc.Status = File.Exists(GetFullPath(doc)) ? DocumentStatus.Active : DocumentStatus.Missing;
        doc.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Restored", "Document", documentId, doc.DisplayId, null, new { doc.Status });
        await _context.SaveChangesAsync();
    }

    public Task DeleteDocumentAsync(Guid documentId) => ArchiveDocumentAsync(documentId);

    public async Task CheckMissingFilesAsync()
    {
        var docs = await _context.Documents.Where(d => d.Status == DocumentStatus.Active).ToListAsync();
        foreach (var doc in docs)
        {
            if (!File.Exists(GetFullPath(doc)))
            {
                doc.Status = DocumentStatus.Missing;
            }
        }
        await _context.SaveChangesAsync();
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        _ => "application/octet-stream"
    };
}
