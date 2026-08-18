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

    public string GetFullPath(Document document) => Path.Combine(_dataLocation.DocumentsPath, document.RelativePath);

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
