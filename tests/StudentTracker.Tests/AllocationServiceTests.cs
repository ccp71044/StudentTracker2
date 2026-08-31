using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class AllocationServiceTests
{
    private static (StudentTrackerDbContext Context, AllocationService Service) CreateService()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budget = new BudgetService(context, gen, audit);
        var service = new AllocationService(context, gen, audit, budget);
        return (context, service);
    }

    [Fact]
    public async Task AllocateStudentAsync_DuplicateActiveAllocation_Throws()
    {
        var (context, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var student = new Student { FirstName = "A", LastName = "B", Email = "a@example.com" };
        context.Students.Add(student);
        context.SaveChanges();

        await service.AllocateStudentAsync(delivery.Id, student.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AllocateStudentAsync(delivery.Id, student.Id));
        Assert.Contains("already allocated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllocateStudentAsync_CancelledAllocation_AllowsReallocation()
    {
        var (context, service) = CreateService();

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, DisplayId = "DEL-0001" };
        context.CourseDeliveries.Add(delivery);
        var student = new Student { FirstName = "A", LastName = "B", Email = "a@example.com" };
        context.Students.Add(student);
        context.SaveChanges();

        var first = await service.AllocateStudentAsync(delivery.Id, student.Id);
        first.AllocationStatus = AllocationStatus.Cancelled;
        await context.SaveChangesAsync();

        var second = await service.AllocateStudentAsync(delivery.Id, student.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
