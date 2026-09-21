using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class InvoicerServiceTests
{
    private static InvoicerService CreateService(StudentTrackerDbContext context, string dataRoot)
    {
        var settings = new AppSettings { DataRootPath = dataRoot };
        var location = new DataLocationService(settings);
        location.EnsureDirectories();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        return new InvoicerService(context, location, gen, audit);
    }

    [Fact]
    public async Task WF006_ExportSameAllocationsTwice_SecondBatchIsEmpty()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataRoot);
        try
        {
            using var context = TestDbContextFactory.Create();
            context.AppSettings.Add(new AppSettings { DataRootPath = dataRoot });
            context.SaveChanges();
            var service = CreateService(context, dataRoot);

            var student = new Student { FirstName = "A", LastName = "B" };
            context.Students.Add(student);
            var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course", DefaultCertificateCost = 100m };
            context.CourseDefinitions.Add(course);
            var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
            context.CourseDeliveries.Add(delivery);
            var a1 = new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, IsBillable = true, CertificateCost = 100m };
            var a2 = new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, IsBillable = true, CertificateCost = 100m };
            context.Allocations.AddRange(a1, a2);
            context.SaveChanges();

            var first = await service.ExportAsync(new List<Guid> { a1.Id, a2.Id });
            Assert.Equal(2, first.ItemCount);

            var second = await service.ExportAsync(new List<Guid> { a1.Id, a2.Id });
            Assert.Equal(0, second.ItemCount);

            Assert.All(context.Allocations, a => Assert.Equal(first.Id, a.ExportedInBatchId));
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { }
        }
    }
}
