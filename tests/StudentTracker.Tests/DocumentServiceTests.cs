using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Tests;

public class DocumentServiceTests
{
    [Fact]
    public async Task UpdateMetadata_UpdatesEditableFieldsAndAudits()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        var document = new Document { DisplayId = "DOC-0001", OriginalFileName = "old.pdf", StoredFileName = "stored.pdf", RelativePath = "General/stored.pdf", DisplayName = "Old" };
        context.Documents.Add(document);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var received = new DateTime(2025, 1, 2);

        await service.UpdateMetadataAsync(document.Id, "  New name  ", "Description", received, "Internal", "Notes");

        Assert.Equal("New name", document.DisplayName);
        Assert.Equal("Description", document.Description);
        Assert.Equal(received, document.ReceivedDate);
        Assert.Equal("Internal", document.Confidentiality);
        Assert.Contains(context.AuditLogs, a => a.Action == "Updated" && a.EntityId == document.Id);
    }

    [Fact]
    public async Task LinkDocument_RejectsDuplicateLink()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        var document = new Document { OriginalFileName = "test.pdf", StoredFileName = "stored.pdf", RelativePath = "General/stored.pdf" };
        var student = new Student { FirstName = "Test", LastName = "Student" };
        context.AddRange(document, student);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.LinkDocumentAsync(document.Id, "Student", student.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LinkDocumentAsync(document.Id, "Student", student.Id));
    }

    [Fact]
    public void LinkTarget_FriendlyNameUsesDisplayIdAndDescription()
    {
        Assert.Equal("STU-0001 — Alex Sample", new DocumentLinkTarget(Guid.NewGuid(), "STU-0001", "Alex Sample").FriendlyName);
        Assert.Equal("STU-0001", new DocumentLinkTarget(Guid.NewGuid(), "STU-0001", string.Empty).FriendlyName);
    }

    private static DocumentService CreateService(StudentTracker.Data.StudentTrackerDbContext context)
    {
        var settings = new AppSettings { DataRootPath = Path.GetTempPath() };
        return new DocumentService(context, new DataLocationService(settings), new DisplayIdGenerator(context), new AuditService(context));
    }
}
