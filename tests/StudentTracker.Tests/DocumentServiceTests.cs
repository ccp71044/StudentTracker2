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
    public async Task RecordCertificateDelivery_LinksOneEvidenceDocumentToFullRecordChainAndAudits()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new() { BillableTrigger = "Manual" });
        var student = new Student { FirstName = "Test", LastName = "Student" };
        var allocation = new Allocation { Student = student };
        var order = new CertificateOrder { Allocation = allocation, Provider = "Provider" };
        var document = new Document
        {
            DisplayId = "DOC-0001", OriginalFileName = "certificate.pdf", StoredFileName = "stored.pdf",
            RelativePath = "Certificates/stored.pdf", DisplayName = "Issued certificate"
        };
        context.AddRange(order, document);
        await context.SaveChangesAsync();
        var audit = new AuditService(context);
        var ids = new DisplayIdGenerator(context);
        var documentService = CreateService(context);
        var creditService = new CreditService(context, ids, audit, documentService);
        var certificateService = new CertificateService(context, ids, creditService, audit);

        var delivery = await certificateService.RecordDeliveryAsync(order.Id, DateTime.UtcNow, "Post", "Student", evidenceDocumentId: document.Id);

        Assert.Equal(document.Id, delivery.EvidenceDocumentId);
        var links = context.DocumentLinks.Where(l => l.DocumentId == document.Id).ToList();
        Assert.Contains(links, l => l.EntityType == "CertificateDelivery" && l.EntityId == delivery.Id);
        Assert.Contains(links, l => l.EntityType == "CertificateOrder" && l.EntityId == order.Id);
        Assert.Contains(links, l => l.EntityType == "Allocation" && l.EntityId == allocation.Id);
        Assert.Contains(links, l => l.EntityType == "Student" && l.EntityId == student.Id);
        Assert.Equal(4, links.Count);
        Assert.Contains(context.AuditLogs, a => a.Action == "Delivered" && a.EntityId == delivery.Id);
        Assert.Equal(4, context.AuditLogs.Count(a => a.Action == "Linked" && a.EntityType == "DocumentLink"));
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
