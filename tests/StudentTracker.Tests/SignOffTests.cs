using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class SignOffTests
{
    [Fact]
    public void GenerateSignOff_CreatesParticipants()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new SignOffService(context, gen, audit);

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id, TrainerName = "Trainer" };
        context.CourseDeliveries.Add(delivery);
        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var alloc = new Allocation { CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        var signOff = service.GenerateDraftAsync(delivery.Id, new List<Guid> { alloc.Id }, "Trainer").Result;

        Assert.Single(signOff.Participants);
        Assert.Equal("S A", signOff.Participants[0].StudentDisplayName);
    }
}
