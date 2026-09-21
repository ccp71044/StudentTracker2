using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class DisplayIdReconciliationServiceTests
{
    private static (StudentTrackerDbContext Context, DisplayIdReconciliationService Service) CreateService()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var audit = new AuditService(context);
        var service = new DisplayIdReconciliationService(context, audit);
        return (context, service);
    }

    [Fact]
    public async Task Resequence_BudgetTransaction_RenumberedByCreatedAt()
    {
        var (context, service) = CreateService();

        var pool = new BudgetPool { Name = "Pool" };
        context.BudgetPools.Add(pool);

        var first = new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 10, DisplayId = "BUD-0005", CreatedAt = new DateTime(2026, 1, 1) };
        var second = new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.CommitmentCreated, Amount = 5, DisplayId = "BUD-0002", CreatedAt = new DateTime(2026, 1, 2) };
        context.BudgetTransactions.AddRange(first, second);
        context.SaveChanges();

        var changed = await service.ResequenceAsync<BudgetTransaction>("BUD");

        Assert.Equal(1, changed);
        Assert.Equal("BUD-0001", first.DisplayId);
        Assert.Equal("BUD-0002", second.DisplayId);
    }

    [Fact]
    public async Task Check_DuplicateDisplayIds_Reported()
    {
        var (context, service) = CreateService();

        var pool = new BudgetPool { Name = "Pool" };
        context.BudgetPools.Add(pool);

        context.BudgetTransactions.AddRange(
            new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 10, DisplayId = "BUD-0001" },
            new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 20, DisplayId = "BUD-0001" });
        context.SaveChanges();

        var report = await service.CheckAsync();

        Assert.Single(report.BudgetTransactionDuplicates);
        Assert.Empty(report.CertificateCreditTransactionDuplicates);
    }
}
