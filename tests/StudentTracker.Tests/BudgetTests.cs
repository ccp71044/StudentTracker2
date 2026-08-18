using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class BudgetTests
{
    [Fact]
    public void AddFundsAndCommit_ForecastReflectsPending()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new BudgetService(context, gen, audit);

        var pool = service.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        service.AddFundsAsync(pool.Id, 1000m).Wait();
        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        var alloc = new Allocation { CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        service.CreateCommitmentAsync(pool.Id, alloc.Id, 200m).Wait();

        Assert.Equal(1000m, service.GetActualAvailableAsync(pool.Id).Result);
        Assert.Equal(800m, service.GetForecastAvailableAsync(pool.Id).Result);
    }
}
