using StudentTracker.Core.Enums;
using StudentTracker.Services;

namespace StudentTracker.Tests;

/// <summary>
/// Covers the certificate credit balance rules in design section 10.1: every figure is derived
/// from transactions, and no event may be counted twice.
/// </summary>
public class CreditLedgerTests
{
    private static async Task<(TestHarness Harness, Guid PoolId, Guid AllocationId)> SetupAsync(decimal topUp = 10m, decimal cost = 100m)
    {
        var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, topUp, topUp);
        var delivery = harness.AddDelivery(cost);
        var student = harness.AddStudent();
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, student.Id, cost, null, pool.Id);
        return (harness, pool.Id, allocation.Id);
    }

    [Fact]
    public async Task TopUp_IncreasesLoadedAndAvailable()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();

        await harness.Credits.TopUpAsync(pool.Id, 25m, 25m);

        var balance = await harness.Credits.GetBalanceAsync(pool.Id);
        Assert.Equal(25m, balance.Loaded);
        Assert.Equal(25m, balance.Available);
        Assert.Equal(0m, balance.Allocated);
    }

    [Fact]
    public async Task Allocate_MovesCreditFromAvailableToAllocated()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;

        await harness.Credits.AllocateAsync(poolId, allocationId, 1m);

        var balance = await harness.Credits.GetBalanceAsync(poolId);
        Assert.Equal(1m, balance.Allocated);
        Assert.Equal(9m, balance.Available);
    }

    [Fact]
    public async Task Release_ReturnsCreditToAvailable()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;
        await harness.Credits.AllocateAsync(poolId, allocationId, 1m);

        await harness.Credits.ReleaseAsync(poolId, allocationId, 1m, "Withdrawn in time");

        var balance = await harness.Credits.GetBalanceAsync(poolId);
        Assert.Equal(0m, balance.Allocated);
        Assert.Equal(10m, balance.Available);
    }

    /// <summary>
    /// Regression: consumption used to be subtracted while the original allocation stayed active,
    /// so ordering a certificate deducted the credit twice.
    /// </summary>
    [Fact]
    public async Task Consume_DeductsCreditExactlyOnce()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;
        await harness.Credits.AllocateAsync(poolId, allocationId, 1m);

        await harness.Credits.ConsumeAsync(poolId, allocationId, 1m, 1, "Certificate ordered");

        var balance = await harness.Credits.GetBalanceAsync(poolId);
        Assert.Equal(1m, balance.Consumed);
        Assert.Equal(0m, balance.Allocated);
        Assert.Equal(9m, balance.Available);
    }

    [Fact]
    public async Task CreditLoss_IsReportedAsUnavailableNotConsumed()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;
        await harness.Credits.AllocateAsync(poolId, allocationId, 1m);

        await harness.Credits.MarkUnavailableAsync(poolId, allocationId, 1m, "Late withdrawal");

        var balance = await harness.Credits.GetBalanceAsync(poolId);
        Assert.Equal(1m, balance.Unavailable);
        Assert.Equal(0m, balance.Consumed);
        Assert.Equal(0m, balance.Allocated);
        Assert.Equal(9m, balance.Available);
    }

    [Fact]
    public async Task MarkUnavailable_RequiresReason()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Credits.MarkUnavailableAsync(poolId, allocationId, 1m, "  "));
    }

    [Fact]
    public async Task Expire_ReducesAvailable()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);

        await harness.Credits.ExpireAsync(pool.Id, 4m, "Validity period ended");

        var balance = await harness.Credits.GetBalanceAsync(pool.Id);
        Assert.Equal(4m, balance.Expired);
        Assert.Equal(6m, balance.Available);
    }

    [Fact]
    public async Task Adjustment_AppliesSignedAmountAndRequiresReason()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);

        await harness.Credits.AdjustAsync(pool.Id, -3m, "Supplier correction");
        Assert.Equal(7m, (await harness.Credits.GetBalanceAsync(pool.Id)).Available);

        await harness.Credits.AdjustAsync(pool.Id, 5m, "Goodwill credit");
        Assert.Equal(12m, (await harness.Credits.GetBalanceAsync(pool.Id)).Available);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Credits.AdjustAsync(pool.Id, 1m, ""));
    }

    [Fact]
    public async Task Reversal_UndoesTransactionAndCannotBeAppliedTwice()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);
        var expiry = await harness.Credits.ExpireAsync(pool.Id, 4m, "Entered in error");

        await harness.Credits.ReverseAsync(expiry.Id, "Expiry entered against the wrong pool");

        var balance = await harness.Credits.GetBalanceAsync(pool.Id);
        Assert.Equal(0m, balance.Expired);
        Assert.Equal(10m, balance.Available);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Credits.ReverseAsync(expiry.Id, "Second attempt"));
    }

    [Fact]
    public async Task Reversal_PreservesTheOriginalTransaction()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        var topUp = await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);

        await harness.Credits.ReverseAsync(topUp.Id, "Duplicate entry");

        Assert.NotNull(await harness.Context.CertificateCreditTransactions.FindAsync(topUp.Id));
    }

    [Fact]
    public async Task OverAllocation_IsBlockedWithoutAnOverrideReason()
    {
        var (harness, poolId, allocationId) = await SetupAsync(topUp: 1m);
        using var _ = harness;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Credits.AllocateAsync(poolId, allocationId, 2m));
    }

    [Fact]
    public async Task OverAllocation_IsPermittedWithAnOverrideReason()
    {
        var (harness, poolId, allocationId) = await SetupAsync(topUp: 1m);
        using var _ = harness;

        await harness.Credits.AllocateAsync(poolId, allocationId, 2m, 1, null, "Top-up invoiced, awaiting supplier load");

        Assert.Equal(-1m, (await harness.Credits.GetBalanceAsync(poolId)).Available);
    }

    [Fact]
    public async Task Reallocation_MovesCreditBetweenPoolsWithoutChangingTheTotal()
    {
        using var harness = new TestHarness();
        var source = await harness.CreditPoolAsync("Source");
        var target = await harness.CreditPoolAsync("Target");
        await harness.Credits.TopUpAsync(source.Id, 10m, 10m);
        await harness.Credits.TopUpAsync(target.Id, 10m, 10m);

        var delivery = harness.AddDelivery();
        var first = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent("A", "One").Id, 100m, null, source.Id);
        var second = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent("B", "Two").Id, 100m, null, target.Id);
        await harness.Credits.AllocateAsync(source.Id, first.Id, 1m);

        await harness.Credits.ReallocateAsync(source.Id, target.Id, first.Id, second.Id, 1m, "Student moved course");

        Assert.Equal(10m, (await harness.Credits.GetBalanceAsync(source.Id)).Available);
        Assert.Equal(9m, (await harness.Credits.GetBalanceAsync(target.Id)).Available);
    }

    [Fact]
    public async Task ConsumedWithoutCompletion_IsSurfacedForReconciliation()
    {
        var (harness, poolId, allocationId) = await SetupAsync();
        using var _ = harness;
        await harness.Credits.AllocateAsync(poolId, allocationId, 1m);
        await harness.Credits.ConsumeAsync(poolId, allocationId, 1m, 1, "Ordered early", CreditTransactionType.OrderConsume);

        var anomalies = await harness.Credits.GetConsumedWithoutCompletionAsync();

        Assert.Contains(anomalies, t => t.AllocationId == allocationId);
    }
}
