using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class BudgetTests
{
    private static StudentTrackerDbContext CreateContext()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task AddFundsAndCommit_ForecastReflectsPending()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new BudgetService(context, gen, audit);

        var pool = await service.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await service.AddFundsAsync(pool.Id, 1000m);
        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        var alloc = new Allocation { CourseDeliveryId = delivery.Id, StudentId = student.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        await service.CreateCommitmentAsync(pool.Id, alloc.Id, 200m);

        Assert.Equal(1000m, await service.GetActualAvailableAsync(pool.Id));
        Assert.Equal(800m, await service.GetForecastAvailableAsync(pool.Id));
    }

    [Fact]
    public async Task CreatePlaceholderAllocations_AtomicallyReservesBudget()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Client Fund", Category = BudgetPoolCategory.ClientFunded, ClientName = "Acme" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var allocations = await allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Group booking", 3, 200m, pool.Id);

        Assert.Equal(3, allocations.Count);
        Assert.All(allocations, a => Assert.Equal(CashCommitmentStatus.Pending, a.CashCommitmentStatus));
        Assert.Equal(1000m, await budgetService.GetActualAvailableAsync(pool.Id));
        Assert.Equal(400m, await budgetService.GetForecastAvailableAsync(pool.Id));
        Assert.Equal(600m, await budgetService.GetPendingCommitmentsAsync(pool.Id));
    }

    [Fact]
    public async Task CreatePlaceholderAllocations_InsufficientFunds_IsBlocked()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Small Budget" });
        await budgetService.AddFundsAsync(pool.Id, 100m);

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Group booking", 2, 200m, pool.Id));
        Assert.Contains("Insufficient budget funds", ex.Message);
    }

    [Fact]
    public async Task ReplacePlaceholder_PreservesBudgetCommitment()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var placeholder = (await allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Placeholder", 1, 250m, pool.Id)).Single();
        Assert.Equal(CashCommitmentStatus.Pending, placeholder.CashCommitmentStatus);

        var replaced = await allocationService.ReplacePlaceholderAsync(placeholder.Id, student.Id);
        Assert.Equal(CashCommitmentStatus.Pending, replaced.CashCommitmentStatus);
        Assert.Equal(250m, await budgetService.GetPendingCommitmentsAsync(pool.Id));
    }

    [Fact]
    public async Task ReleaseCommitment_RestoresForecast()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = await allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id);
        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);
        Assert.Equal(700m, await budgetService.GetForecastAvailableAsync(pool.Id));

        await allocationService.ReleaseCommitmentAsync(alloc.Id);
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(1000m, await budgetService.GetForecastAvailableAsync(pool.Id));
        Assert.Equal(0m, await budgetService.GetPendingCommitmentsAsync(pool.Id));
    }

    [Fact]
    public async Task MarkCostSpent_RecognisesExpense()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = await allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id);
        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);
        await allocationService.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed);

        await allocationService.MarkCostSpentAsync(alloc.Id);
        Assert.Equal(CashCommitmentStatus.Spent, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(300m, await budgetService.GetActualExpenditureAsync(pool.Id));
        Assert.Equal(700m, await budgetService.GetActualAvailableAsync(pool.Id));
        Assert.Equal(0m, await budgetService.GetPendingCommitmentsAsync(pool.Id));
    }

    [Fact]
    public async Task MarkCostSpent_BlocksIfNotCompletedUnlessForced()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = await allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id);
        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => allocationService.MarkCostSpentAsync(alloc.Id));
        Assert.Contains("override", ex.Message);

        await allocationService.MarkCostSpentAsync(alloc.Id, force: true);
        Assert.Equal(CashCommitmentStatus.Spent, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
    }

    [Fact]
    public async Task ReverseSpentCost_RestoresActualFunds()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = await allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id);
        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);
        await allocationService.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed);
        await allocationService.MarkCostSpentAsync(alloc.Id);

        await allocationService.ReverseSpentCostAsync(alloc.Id);
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(0m, await budgetService.GetActualExpenditureAsync(pool.Id));
        Assert.Equal(1000m, await budgetService.GetActualAvailableAsync(pool.Id));
    }

    [Fact]
    public async Task CreateOrRestoreCommitment_CanRestoreReleasedCommitment()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = await budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" });
        await budgetService.AddFundsAsync(pool.Id, 1000m);

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = await allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id);
        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);
        await allocationService.ReleaseCommitmentAsync(alloc.Id);
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);

        await allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m);
        Assert.Equal(CashCommitmentStatus.Pending, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(700m, await budgetService.GetForecastAvailableAsync(pool.Id));
    }

    [Fact]
    public async Task BudgetPool_CategoryAndClientName_Persisted()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new BudgetService(context, gen, audit);

        var pool = await service.CreatePoolAsync(new BudgetPool
        {
            Name = "Client Fund",
            Category = BudgetPoolCategory.ClientFunded,
            ClientName = "Acme Corp"
        });

        var reloaded = await service.GetPoolAsync(pool.Id);
        Assert.Equal(BudgetPoolCategory.ClientFunded, reloaded!.Category);
        Assert.Equal("Acme Corp", reloaded.ClientName);
    }
}
