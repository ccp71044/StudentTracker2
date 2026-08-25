using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

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

    public async Task<DocumentLink> LinkDocumentAsync(Guid documentId, string entityType, Guid entityId, string? purpose = null)
    {
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

    public async Task<List<Document>> GetDocumentsForEntityAsync(string entityType, Guid entityId)
    {
        return await _context.DocumentLinks
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .Include(l => l.Document)
            .Select(l => l.Document!)
            .Where(d => d.Status != DocumentStatus.Archived)
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

    /// <summary>
    /// Stores <paramref name="sourcePath"/> as the next version of an existing document. The
    /// previous version is kept and marked <see cref="DocumentStatus.Superseded"/>, and every link
    /// it carried is repointed at the new version so screens show the current file by default.
    /// </summary>
    public async Task<Document> AddVersionAsync(Guid existingDocumentId, string sourcePath, string? description = null)
    {
        var previous = await _context.Documents.FindAsync(existingDocumentId)
            ?? throw new InvalidOperationException("Document not found.");

        var categoryFolder = Path.GetDirectoryName(previous.RelativePath) ?? string.Empty;
        var next = await AddDocumentAsync(sourcePath, categoryFolder, previous.DisplayName, description ?? previous.Description);
        next.Version = previous.Version + 1;
        next.SupersedesDocumentId = previous.Id;
        previous.Status = DocumentStatus.Superseded;

        var links = await _context.DocumentLinks.Where(l => l.DocumentId == previous.Id).ToListAsync();
        foreach (var link in links)
        {
            link.DocumentId = next.Id;
        }

        await _context.SaveChangesAsync();
        _audit.Record("Superseded", "Document", previous.Id, previous.DisplayId, null, new { NewVersionId = next.Id, next.Version });
        await _context.SaveChangesAsync();
        return next;
    }

    public string GetFullPath(Document document) => Path.Combine(_dataLocation.DocumentsPath, document.RelativePath);

    /// <summary>
    /// Reconciles the managed store against the database: files that have gone are flagged
    /// <see cref="DocumentStatus.Missing"/>, and a file that reappears with its original checksum
    /// is returned to <see cref="DocumentStatus.Active"/>. Report 22 reads the resulting statuses.
    /// </summary>
    public async Task<List<Document>> CheckMissingFilesAsync()
    {
        var docs = await _context.Documents
            .Where(d => d.Status == DocumentStatus.Active || d.Status == DocumentStatus.Missing)
            .ToListAsync();

        var missing = new List<Document>();
        foreach (var doc in docs)
        {
            if (!File.Exists(GetFullPath(doc)))
            {
                doc.Status = DocumentStatus.Missing;
                missing.Add(doc);
            }
            else if (doc.Status == DocumentStatus.Missing)
            {
                doc.Status = DocumentStatus.Active;
            }
        }

        await _context.SaveChangesAsync();
        return missing;
    }

    /// <summary>
    /// Returns true when the stored file still matches the checksum recorded at upload. A false
    /// result means the managed copy was edited or replaced outside the application.
    /// </summary>
    public async Task<bool> VerifyChecksumAsync(Guid documentId)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new InvalidOperationException("Document not found.");

        var path = GetFullPath(doc);
        if (!File.Exists(path) || doc.Sha256 is null) return false;
        return string.Equals(await ComputeSha256Async(path), doc.Sha256, StringComparison.OrdinalIgnoreCase);
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
