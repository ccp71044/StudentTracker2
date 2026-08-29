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
    public void AddFundsAndCommit_ForecastReflectsPending()
    {
        using var context = CreateContext();
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

    [Fact]
    public void CreatePlaceholderAllocations_AtomicallyReservesBudget()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Client Fund", Category = BudgetPoolCategory.ClientFunded, ClientName = "Acme" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var allocations = allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Group booking", 3, 200m, pool.Id).Result;

        Assert.Equal(3, allocations.Count);
        Assert.All(allocations, a => Assert.Equal(CashCommitmentStatus.Pending, a.CashCommitmentStatus));
        Assert.Equal(1000m, budgetService.GetActualAvailableAsync(pool.Id).Result);
        Assert.Equal(400m, budgetService.GetForecastAvailableAsync(pool.Id).Result);
        Assert.Equal(600m, budgetService.GetPendingCommitmentsAsync(pool.Id).Result);
    }

    [Fact]
    public void CreatePlaceholderAllocations_InsufficientFunds_IsBlocked()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Small Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 100m).Wait();

        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var ex = Assert.Throws<AggregateException>(() => allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Group booking", 2, 200m, pool.Id).Wait());
        Assert.Contains("Insufficient budget funds", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public void ReplacePlaceholder_PreservesBudgetCommitment()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var placeholder = allocationService.CreatePlaceholderAllocationsAsync(delivery.Id, "Placeholder", 1, 250m, pool.Id).Result.Single();
        Assert.Equal(CashCommitmentStatus.Pending, placeholder.CashCommitmentStatus);

        var replaced = allocationService.ReplacePlaceholderAsync(placeholder.Id, student.Id).Result;
        Assert.Equal(CashCommitmentStatus.Pending, replaced.CashCommitmentStatus);
        Assert.Equal(250m, budgetService.GetPendingCommitmentsAsync(pool.Id).Result);
    }

    [Fact]
    public void ReleaseCommitment_RestoresForecast()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id).Result;
        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();
        Assert.Equal(700m, budgetService.GetForecastAvailableAsync(pool.Id).Result);

        allocationService.ReleaseCommitmentAsync(alloc.Id).Wait();
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(1000m, budgetService.GetForecastAvailableAsync(pool.Id).Result);
        Assert.Equal(0m, budgetService.GetPendingCommitmentsAsync(pool.Id).Result);
    }

    [Fact]
    public void MarkCostSpent_RecognisesExpense()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id).Result;
        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();
        allocationService.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed).Wait();

        allocationService.MarkCostSpentAsync(alloc.Id).Wait();
        Assert.Equal(CashCommitmentStatus.Spent, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(300m, budgetService.GetActualExpenditureAsync(pool.Id).Result);
        Assert.Equal(700m, budgetService.GetActualAvailableAsync(pool.Id).Result);
        Assert.Equal(0m, budgetService.GetPendingCommitmentsAsync(pool.Id).Result);
    }

    [Fact]
    public void MarkCostSpent_BlocksIfNotCompletedUnlessForced()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id).Result;
        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();

        var ex = Assert.Throws<AggregateException>(() => allocationService.MarkCostSpentAsync(alloc.Id).Wait());
        Assert.Contains("override", ex.InnerException?.Message ?? ex.Message);

        allocationService.MarkCostSpentAsync(alloc.Id, force: true).Wait();
        Assert.Equal(CashCommitmentStatus.Spent, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
    }

    [Fact]
    public void ReverseSpentCost_RestoresActualFunds()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id).Result;
        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();
        allocationService.MarkOutcomeAsync(alloc.Id, OutcomeStatus.Completed).Wait();
        allocationService.MarkCostSpentAsync(alloc.Id).Wait();

        allocationService.ReverseSpentCostAsync(alloc.Id).Wait();
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(0m, budgetService.GetActualExpenditureAsync(pool.Id).Result);
        Assert.Equal(1000m, budgetService.GetActualAvailableAsync(pool.Id).Result);
    }

    [Fact]
    public void CreateOrRestoreCommitment_CanRestoreReleasedCommitment()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var budgetService = new BudgetService(context, gen, audit);
        var allocationService = new AllocationService(context, gen, audit, budgetService);

        var pool = budgetService.CreatePoolAsync(new BudgetPool { Name = "Budget" }).Result;
        budgetService.AddFundsAsync(pool.Id, 1000m).Wait();

        var student = new Student { FirstName = "S", LastName = "A" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        context.SaveChanges();

        var alloc = allocationService.AllocateStudentAsync(delivery.Id, student.Id, 300m, pool.Id).Result;
        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();
        allocationService.ReleaseCommitmentAsync(alloc.Id).Wait();
        Assert.Equal(CashCommitmentStatus.Released, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);

        allocationService.CreateOrRestoreCommitmentAsync(alloc.Id, 300m).Wait();
        Assert.Equal(CashCommitmentStatus.Pending, context.Allocations.Find(alloc.Id)!.CashCommitmentStatus);
        Assert.Equal(700m, budgetService.GetForecastAvailableAsync(pool.Id).Result);
    }

    [Fact]
    public void BudgetPool_CategoryAndClientName_Persisted()
    {
        using var context = CreateContext();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var service = new BudgetService(context, gen, audit);

        var pool = service.CreatePoolAsync(new BudgetPool
        {
            Name = "Client Fund",
            Category = BudgetPoolCategory.ClientFunded,
            ClientName = "Acme Corp"
        }).Result;

        var reloaded = service.GetPoolAsync(pool.Id).Result;
        Assert.Equal(BudgetPoolCategory.ClientFunded, reloaded!.Category);
        Assert.Equal("Acme Corp", reloaded.ClientName);
    }
}
