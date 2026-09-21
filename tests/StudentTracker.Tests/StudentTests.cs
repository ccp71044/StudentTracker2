using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class StudentTests
{
    [Fact]
    public async Task CreateStudent_AssignsDisplayId()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new StudentService(context, gen, audit);

        var student = await service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" });

        Assert.NotNull(student.DisplayId);
        Assert.StartsWith("STU-", student.DisplayId);
    }

    [Fact]
    public async Task DuplicateName_FlagsPotentialDuplicate()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new StudentService(context, gen, audit);

        await service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" });
        var second = await service.CreateAsync(new Student { FirstName = "Alex", LastName = "Sample", Email = "b@example.com" });

        Assert.True(second.PotentialDuplicate);
    }

    [Fact]
    public async Task ArchiveAndRestore_ControlsStudentVisibilityAndCreatesAuditRecords()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        await context.SaveChangesAsync();
        var service = new StudentService(context, new DisplayIdGenerator(context), new AuditService(context));
        var student = await service.CreateAsync(new Student { FirstName = "Lifecycle", LastName = "Test" });

        await service.ArchiveAsync(student.Id);
        Assert.Empty(await service.SearchAsync(null));
        Assert.Single(await service.SearchAsync(null, true));

        await service.ArchiveAsync(student.Id, false);
        Assert.Single(await service.SearchAsync(null));
        Assert.Contains(context.AuditLogs, e => e.Action == "Archived" && e.EntityId == student.Id);
        Assert.Contains(context.AuditLogs, e => e.Action == "Restored" && e.EntityId == student.Id);
    }
}
