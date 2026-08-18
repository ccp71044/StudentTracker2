using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class StudentTests
{
    [Fact]
    public void CreateStudent_AssignsDisplayId()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new StudentService(context, gen, audit);

        var student = service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" }).Result;

        Assert.NotNull(student.DisplayId);
        Assert.StartsWith("STU-", student.DisplayId);
    }

    [Fact]
    public void DuplicateName_FlagsPotentialDuplicate()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new StudentService(context, gen, audit);

        service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" }).Wait();
        var second = service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "b@example.com" }).Result;

        Assert.True(second.PotentialDuplicate);
    }
}
