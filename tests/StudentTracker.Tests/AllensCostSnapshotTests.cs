using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class AllensCostSnapshotTests
{
    private static (StudentTrackerDbContext Context, AllocationService Allocation, BudgetService Budget) CreateService()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budget = new BudgetService(context, gen, audit);
        var allocation = new AllocationService(context, gen, audit, budget);
        return (context, allocation, budget);
    }

    [Fact]
    public async Task MP014_AllensCost_Snapshotted_And_Retained_When_Default_Changes()
    {
        var (context, allocation, budget) = CreateService();

        var course = new CourseDefinition
        {
            CourseCode = "HLTAID011",
            CourseTitle = "Provide First Aid",
            DefaultCertificateCost = 30m,
            DefaultAllensCost = 20m
        };
        context.CourseDefinitions.Add(course);

        var delivery = new CourseDelivery
        {
            CourseDefinitionId = course.Id,
            DisplayId = "DEL-0001"
        };
        context.CourseDeliveries.Add(delivery);

        var student = new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" };
        context.Students.Add(student);

        var pool = await budget.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budget.AddFundsAsync(pool.Id, 1000m);

        await context.SaveChangesAsync();

        var alloc = await allocation.AllocateStudentAsync(delivery.Id, student.Id, budgetPoolId: pool.Id, createCashCommitment: true);

        Assert.Equal(20m, alloc.AllensCostAtAllocation);

        course.DefaultAllensCost = 25m;
        context.CourseDefinitions.Update(course);
        await context.SaveChangesAsync();

        var reloaded = await context.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.Id == alloc.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(20m, reloaded.AllensCostAtAllocation);
    }

    [Fact]
    public async Task MarkCostSpent_Records_ActualAllensCost_From_Snapshot()
    {
        var (context, allocation, budget) = CreateService();

        var course = new CourseDefinition
        {
            CourseCode = "HLTAID011",
            CourseTitle = "Provide First Aid",
            DefaultCertificateCost = 30m,
            DefaultAllensCost = 20m
        };
        context.CourseDefinitions.Add(course);

        var delivery = new CourseDelivery
        {
            CourseDefinitionId = course.Id,
            DisplayId = "DEL-0001"
        };
        context.CourseDeliveries.Add(delivery);

        var student = new Student { FirstName = "Alex", LastName = "Sample", Email = "a@example.com" };
        context.Students.Add(student);

        var pool = await budget.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budget.AddFundsAsync(pool.Id, 1000m);

        await context.SaveChangesAsync();

        var alloc = await allocation.AllocateStudentAsync(delivery.Id, student.Id, budgetPoolId: pool.Id);
        await allocation.CreateOrRestoreCommitmentAsync(alloc.Id, 30m);
        await allocation.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed);
        await allocation.MarkCostSpentAsync(alloc.Id);

        var reloaded = await context.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.Id == alloc.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(20m, reloaded.ActualAllensCost);
    }
}
