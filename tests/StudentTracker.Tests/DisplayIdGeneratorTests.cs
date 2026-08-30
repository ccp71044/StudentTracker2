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

        for (int i = 0; i < 5; i++)
        {
            var tx = new BudgetTransaction
            {
                DisplayId = gen.NextDisplayId<BudgetTransaction>("BTX"),
                PoolId = Guid.NewGuid(),
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

        for (int i = 0; i < 5; i++)
        {
            var tx = new CertificateCreditTransaction
            {
                DisplayId = gen.NextDisplayId<CertificateCreditTransaction>("CTX"),
                PoolId = Guid.NewGuid(),
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
