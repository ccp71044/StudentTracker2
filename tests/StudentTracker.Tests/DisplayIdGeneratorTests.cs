using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class DisplayIdGeneratorTests
{
    [Fact]
    public async Task NextDisplayId_BulkBudgetTransactions_AreSequential()
    {
        using var context = TestDbContextFactory.Create();
        var gen = new DisplayIdGenerator(context);
        var pool = new BudgetPool { Name = "Pool" };
        context.BudgetPools.Add(pool);
        context.SaveChanges();

        for (int i = 0; i < 5; i++)
        {
            var tx = new BudgetTransaction
            {
                DisplayId = gen.NextDisplayId<BudgetTransaction>("BTX"),
                PoolId = pool.Id,
                TransactionType = BudgetTransactionType.FundsAdded,
                Amount = 100m
            };
            context.BudgetTransactions.Add(tx);
        }

        await context.SaveChangesAsync();

        var ids = context.BudgetTransactions
            .Select(t => t.DisplayId)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(new[] { "BTX-0001", "BTX-0002", "BTX-0003", "BTX-0004", "BTX-0005" }, ids);
    }

    [Fact]
    public async Task NextDisplayId_BulkCreditTransactions_AreSequential()
    {
        using var context = TestDbContextFactory.Create();
        var gen = new DisplayIdGenerator(context);
        var pool = new CertificateCreditPool { Name = "Pool" };
        context.CertificateCreditPools.Add(pool);
        context.SaveChanges();

        for (int i = 0; i < 5; i++)
        {
            var tx = new CertificateCreditTransaction
            {
                DisplayId = gen.NextDisplayId<CertificateCreditTransaction>("CTX"),
                PoolId = pool.Id,
                TransactionType = CreditTransactionType.TopUp,
                Amount = 1m,
                Quantity = 1m
            };
            context.CertificateCreditTransactions.Add(tx);
        }

        await context.SaveChangesAsync();

        var ids = context.CertificateCreditTransactions
            .Select(t => t.DisplayId)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(new[] { "CTX-0001", "CTX-0002", "CTX-0003", "CTX-0004", "CTX-0005" }, ids);
    }
}
