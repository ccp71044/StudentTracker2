using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class CreditTopUpReceiptTests
{
    [Fact]
    public async Task TopUpWithReceipt_CreatesTransactionDocumentAndLinks()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        await context.SaveChangesAsync();

        var dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataRoot);
        try
        {
            var service = CreateService(context, dataRoot);
            var pool = await service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" });
            var receiptPath = Path.Combine(dataRoot, "receipt.pdf");
            await File.WriteAllTextAsync(receiptPath, "pdf-content");

            var tx = await service.TopUpWithReceiptAsync(
                pool.Id,
                100m,
                10m,
                new DateTime(2025, 6, 15),
                "REF-123",
                "Allen top-up",
                "June batch",
                receiptPath);

            Assert.Equal(100m, tx.Amount);
            Assert.Equal(10m, tx.Quantity);
            Assert.Equal("REF-123", tx.ExternalPurchaseReference);
            Assert.Equal("Allen top-up", tx.Reason);
            Assert.Equal("June batch", tx.Notes);

            var withReceipts = await service.GetTransactionsWithReceiptsAsync(pool.Id);
            var row = Assert.Single(withReceipts);
            Assert.NotNull(row.Receipt);
            Assert.Equal("receipt.pdf", row.Receipt?.DisplayName);

            var transactionLinks = context.DocumentLinks
                .Where(l => l.EntityType == nameof(CertificateCreditTransaction) && l.EntityId == tx.Id)
                .ToList();
            Assert.Single(transactionLinks);
            Assert.Equal("Receipt", transactionLinks[0].LinkPurpose);

            var poolLinks = context.DocumentLinks
                .Where(l => l.EntityType == nameof(CertificateCreditPool) && l.EntityId == pool.Id)
                .ToList();
            Assert.Single(poolLinks);
            Assert.Equal("PoolReceipt", poolLinks[0].LinkPurpose);

            var doc = row.Receipt!;
            Assert.True(File.Exists(service.GetDocumentFullPath(doc)), "Managed receipt file should exist on disk");

            Assert.Contains(context.AuditLogs, a => a.Action == "TopUpWithReceipt" && a.EntityId == tx.Id);
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task TopUpWithReceipt_WithoutReceipt_CreatesTransactionOnly()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        await context.SaveChangesAsync();

        var service = CreateService(context, Path.GetTempPath());
        var pool = await service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" });

        var tx = await service.TopUpWithReceiptAsync(pool.Id, 50m, reason: "Manual adjustment");

        Assert.Equal(50m, tx.Amount);
        var rows = await service.GetTransactionsWithReceiptsAsync(pool.Id);
        Assert.Single(rows);
        Assert.Null(rows[0].Receipt);
        Assert.DoesNotContain(context.DocumentLinks, l => l.EntityType == nameof(CertificateCreditPool) && l.EntityId == pool.Id);
    }

    [Fact]
    public async Task TopUpWithReceipt_RollsBackAndCleansFile_WhenLinkFails()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<StudentTrackerDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new StudentTrackerDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.AppSettings.Add(new());
        await context.SaveChangesAsync();

        var dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataRoot);
        try
        {
            var fakeDocumentService = new FailingDocumentService(context, dataRoot);
            var service = new CreditService(context, new DisplayIdGenerator(context), new AuditService(context), fakeDocumentService);
            var pool = await service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" });
            var receiptPath = Path.Combine(dataRoot, "receipt.pdf");
            await File.WriteAllTextAsync(receiptPath, "pdf-content");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.TopUpWithReceiptAsync(pool.Id, 25m, receiptFilePath: receiptPath));

            Assert.Empty(context.CertificateCreditTransactions.Where(t => t.PoolId == pool.Id));
            Assert.Empty(context.Documents);
            Assert.Empty(context.DocumentLinks);
            Assert.Empty(fakeDocumentService.CopiedFiles.Where(File.Exists));
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { }
        }
    }

    private static CreditService CreateService(StudentTracker.Data.StudentTrackerDbContext context, string dataRoot)
    {
        var settings = new AppSettings { DataRootPath = dataRoot };
        IDocumentService documentService = new DocumentService(context, new DataLocationService(settings), new DisplayIdGenerator(context), new AuditService(context));
        return new CreditService(context, new DisplayIdGenerator(context), new AuditService(context), documentService);
    }

    private class FailingDocumentService : IDocumentService
    {
        private readonly StudentTracker.Data.StudentTrackerDbContext _context;
        private readonly string _dataRoot;
        private readonly List<string> _copiedFiles = new();

        public FailingDocumentService(StudentTracker.Data.StudentTrackerDbContext context, string dataRoot)
        {
            _context = context;
            _dataRoot = dataRoot;
        }

        public IReadOnlyList<string> CopiedFiles => _copiedFiles;

        public Task<Document> AddDocumentAsync(string sourcePath, string categoryFolder, string? displayName = null, string? description = null, string? mimeType = null, DateTime? receivedDate = null)
        {
            var relative = Path.Combine("Documents", categoryFolder, $"{Guid.NewGuid()}{Path.GetExtension(sourcePath)}");
            var dest = Path.Combine(_dataRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(sourcePath, dest, overwrite: true);
            _copiedFiles.Add(dest);

            var doc = new Document
            {
                DisplayId = "DOC-0001",
                OriginalFileName = Path.GetFileName(sourcePath),
                StoredFileName = Path.GetFileName(dest),
                RelativePath = relative,
                DisplayName = displayName ?? Path.GetFileName(sourcePath),
                Description = description,
                MimeType = mimeType,
                ReceivedDate = receivedDate,
                FileSize = new FileInfo(dest).Length,
                Status = Core.Enums.DocumentStatus.Active
            };
            _context.Documents.Add(doc);
            return Task.FromResult(doc);
        }

        public Task<DocumentLink> LinkDocumentAsync(Guid documentId, string entityType, Guid entityId, string? purpose = null)
        {
            throw new InvalidOperationException("Simulated failure after receipt file copied.");
        }

        public Task<List<Document>> GetDocumentsForEntityAsync(string entityType, Guid entityId, bool includeArchived = false) =>
            Task.FromResult(new List<Document>());

        public string GetFullPath(Document document) => Path.Combine(_dataRoot, document.RelativePath);
    }
}

public class CreditTests
{
    [Fact]
    public async Task TopUpThenAllocate_ConsumesBalance()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var service = CreateService(context);

        var pool = await service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" });
        await service.TopUpAsync(pool.Id, 10m, 10m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        context.SaveChanges();
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course 1" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var alloc = new Allocation
        {
            CourseDeliveryId = delivery.Id,
            StudentId = student.Id,
            DisplayId = "ALL-0001",
            CreditPoolId = pool.Id,
            CreditStatus = CreditStatus.None
        };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await service.AllocateAsync(pool.Id, alloc.Id, 1m);

        var available = await service.GetAvailableAsync(pool.Id);
        Assert.Equal(9m, available);
    }

    [Fact]
    public async Task OverAllocation_IsBlocked()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var service = CreateService(context);

        var pool = await service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" });
        await service.TopUpAsync(pool.Id, 1m, 1m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AllocateAsync(pool.Id, Guid.NewGuid(), 2m));
    }

    private static CreditService CreateService(StudentTracker.Data.StudentTrackerDbContext context)
    {
        var settings = new AppSettings { DataRootPath = Path.GetTempPath() };
        IDocumentService documentService = new DocumentService(context, new DataLocationService(settings), new DisplayIdGenerator(context), new AuditService(context));
        return new CreditService(
            context,
            new DisplayIdGenerator(context),
            new AuditService(context),
            documentService);
    }
}
