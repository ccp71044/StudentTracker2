using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class CreditTests
{
    [Fact]
    public void TopUpThenAllocate_ConsumesBalance()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new CreditService(context, gen, audit);

        var pool = service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" }).Result;
        service.TopUpAsync(pool.Id, 10m, 10m).Wait();

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

        service.AllocateAsync(pool.Id, alloc.Id, 1m).Wait();

        var available = service.GetAvailableAsync(pool.Id).Result;
        Assert.Equal(9m, available);
    }

    [Fact]
    public void OverAllocation_IsBlocked()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new CreditService(context, gen, audit);

        var pool = service.CreatePoolAsync(new CertificateCreditPool { Name = "Pool" }).Result;
        service.TopUpAsync(pool.Id, 1m, 1m).Wait();

        Assert.Throws<AggregateException>(() => service.AllocateAsync(pool.Id, Guid.NewGuid(), 2m).Wait());
    }
}
