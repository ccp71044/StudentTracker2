using System.Text;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class InvoicerReferenceImportTests
{
    private static (StudentTrackerDbContext Context, InvoicerReferenceImportService Service) CreateService()
    {
        var context = TestDbContextFactory.Create();
        var dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataRoot);
        context.AppSettings.Add(new AppSettings { DataRootPath = dataRoot });
        context.SaveChanges();

        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var dataLocation = new DataLocationService(context.AppSettings.First());
        dataLocation.EnsureDirectories();
        var documentService = new DocumentService(context, dataLocation, gen, audit);
        var service = new InvoicerReferenceImportService(context, gen, documentService, audit);
        return (context, service);
    }

    private static string WriteCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    [Fact]
    public async Task MP011_Import_Is_Idempotent()
    {
        var (context, service) = CreateService();

        var csv = "ExternalInvoiceId,InvoiceNumber,Customer,InvoiceDate,DueDate,TotalAmount,GSTAmount,PaymentStatus,AmountAssignedToStudentTracker,PdfPath,Notes\n" +
                  "INV-EXT-001,INV001,T&C,2026-08-01,2026-09-01,1200.00,0.00,Unpaid,1200.00,,First aid top-up\n" +
                  "INV-EXT-002,INV002,Alex,2026-08-02,2026-09-02,600.00,0.00,Paid,600.00,,General pool\n";

        var path = WriteCsv(csv);
        try
        {
            var first = await service.ImportFromFileAsync(path);
            Assert.True(first.Errors.Count == 0, string.Join("; ", first.Errors));
            Assert.Equal(2, first.Total);
            Assert.Equal(2, first.ImportedCount);
            Assert.Equal(0, first.UpdatedCount);
            Assert.Equal(0, first.SkippedCount);

            var second = await service.ImportFromFileAsync(path);
            Assert.Equal(0, second.ImportedCount);
            Assert.Equal(0, second.UpdatedCount);
            Assert.Equal(2, second.SkippedCount);

            var count = await Task.FromResult(context.Invoices.Count());
            Assert.Equal(2, count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_Updates_Existing_When_PaymentStatus_Changes()
    {
        var (context, service) = CreateService();

        var firstCsv = "ExternalInvoiceId,InvoiceNumber,Customer,InvoiceDate,DueDate,TotalAmount,GSTAmount,PaymentStatus,AmountAssignedToStudentTracker,PdfPath,Notes\n" +
                       "INV-EXT-001,INV001,T&C,2026-08-01,2026-09-01,1200.00,0.00,Unpaid,1200.00,,\n";

        var path = WriteCsv(firstCsv);
        try
        {
            var first = await service.ImportFromFileAsync(path);
            Assert.Equal(1, first.ImportedCount);

            var updateCsv = "ExternalInvoiceId,InvoiceNumber,Customer,InvoiceDate,DueDate,TotalAmount,GSTAmount,PaymentStatus,AmountAssignedToStudentTracker,PdfPath,Notes\n" +
                            "INV-EXT-001,INV001,T&C,2026-08-01,2026-09-01,1200.00,0.00,Paid,1200.00,,\n";
            var updatePath = WriteCsv(updateCsv);
            try
            {
                var second = await service.ImportFromFileAsync(updatePath);
                Assert.Equal(0, second.ImportedCount);
                Assert.Equal(1, second.UpdatedCount);
                Assert.Equal("Paid", context.Invoices.First().PaymentStatus);
            }
            finally
            {
                File.Delete(updatePath);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_Matches_By_InvoiceNumber_When_ExternalId_Missing()
    {
        var (context, service) = CreateService();

        var firstCsv = "ExternalInvoiceId,InvoiceNumber,Customer,InvoiceDate,DueDate,TotalAmount,GSTAmount,PaymentStatus,AmountAssignedToStudentTracker,PdfPath,Notes\n" +
                       ",INV001,T&C,2026-08-01,2026-09-01,1200.00,0.00,Unpaid,1200.00,,\n";

        var secondCsv = "ExternalInvoiceId,InvoiceNumber,Customer,InvoiceDate,DueDate,TotalAmount,GSTAmount,PaymentStatus,AmountAssignedToStudentTracker,PdfPath,Notes\n" +
                        ",INV001,T&C,2026-08-01,2026-09-01,1200.00,0.00,Paid,1200.00,,\n";

        var firstPath = WriteCsv(firstCsv);
        var secondPath = WriteCsv(secondCsv);
        try
        {
            var first = await service.ImportFromFileAsync(firstPath);
            Assert.Equal(1, first.ImportedCount);

            var second = await service.ImportFromFileAsync(secondPath);
            Assert.Equal(0, second.ImportedCount);
            Assert.Equal(1, second.UpdatedCount);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }
}
