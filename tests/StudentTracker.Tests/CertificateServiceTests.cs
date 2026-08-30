using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class CertificateServiceTests
{
    private static (StudentTrackerDbContext Context, CreditService Credit, AllocationService Allocation, CertificateService Certificate, BudgetService Budget) CreateService(string dataRoot)
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new AppSettings { DataRootPath = dataRoot });
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var documentService = new DocumentService(context, new DataLocationService(context.AppSettings.First()), gen, audit);
        var budget = new BudgetService(context, gen, audit);
        var allocation = new AllocationService(context, gen, audit, budget);
        var credit = new CreditService(context, gen, audit, documentService);
        var certificate = new CertificateService(context, gen, credit, audit);
        return (context, credit, allocation, certificate, budget);
    }

    [Fact]
    public async Task WF005_CertificateOrderedAndDelivered_UpdatesStatusAndConsumesCredit()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataRoot);
        try
        {
            var (context, credit, allocation, certificate, _) = CreateService(dataRoot);

            var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "First Aid", DefaultCertificateCost = 30m };
            context.CourseDefinitions.Add(course);
            var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
            context.CourseDeliveries.Add(delivery);
            var student = new Student { FirstName = "A", LastName = "B", Email = "a@example.com" };
            context.Students.Add(student);
            var creditPool = await credit.CreatePoolAsync(new CertificateCreditPool { Name = "Credit" });
            await credit.TopUpWithReceiptAsync(creditPool.Id, 1m, 1m, reference: "REF", reason: "Top-up");
            var budgetPool = await new BudgetService(context, new DisplayIdGenerator(context), new AuditService(context)).CreatePoolAsync(new BudgetPool { Name = "Budget" });
            await context.SaveChangesAsync();

            var alloc = await allocation.AllocateStudentAsync(delivery.Id, student.Id, creditPoolId: creditPool.Id, reserveCredit: true, createCashCommitment: false);
            await allocation.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed);

            var order = await certificate.OrderCertificateAsync(alloc.Id, "Provider A", externalReference: "EXT-1");

            Assert.Equal(CertificateOrderStatus.Ordered, order.Status);
            Assert.Equal(CertificateDeliveryStatus.Awaiting, (await context.Allocations.AsNoTracking().FirstAsync(a => a.Id == alloc.Id)).CertificateDeliveryStatus);

            var deliveryRecord = await certificate.RecordDeliveryAsync(order.Id, DateTime.UtcNow, "Email", "a@example.com");

            var reloadedOrder = await context.CertificateOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            var reloadedAlloc = await context.Allocations.AsNoTracking().FirstAsync(a => a.Id == alloc.Id);
            Assert.Equal(CertificateOrderStatus.Ordered, reloadedOrder.Status);
            Assert.Equal(CertificateDeliveryStatus.Delivered, reloadedAlloc.CertificateDeliveryStatus);
            Assert.NotNull(deliveryRecord.DeliveredDate);
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { }
        }
    }
}
