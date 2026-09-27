using StudentTracker.Core.Enums;

namespace StudentTracker.Tests;

/// <summary>
/// Covers the mandatory reports in design section 14 whose filters carry business meaning.
/// </summary>
public class ReportTests
{
    /// <summary>
    /// Regression: the awaiting-order filter used to be mis-bracketed, so allocations flagged
    /// Ready appeared even when the student had never completed the course.
    /// </summary>
    [Fact]
    public async Task AwaitingOrder_ExcludesStudentsWhoHaveNotCompleted()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();
        var pending = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent("Not", "Done").Id);
        pending.CertificateOrderStatus = CertificateOrderStatus.Ready;
        var completed = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent("All", "Done").Id);
        await harness.Context.SaveChangesAsync();
        await harness.Allocations.MarkCompletedAsync(completed.Id);

        var awaiting = await harness.Reports.GetCertificatesAwaitingOrderAsync();

        Assert.Single(awaiting);
        Assert.Equal(completed.Id, awaiting[0].Id);
    }

    [Fact]
    public async Task WithdrawnWithCosts_SeparatesReusableCreditFromLostCredit()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var reason = harness.AddReason("Withdrawal", "Left the company");

        var reusable = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent("Re", "Usable").Id, 200m, null, pool.Id, reserveCredit: true);
        await harness.Allocations.WithdrawAsync(reusable.Id, reason.Id, creditReusable: true, releaseCashCommitment: false);

        var lost = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent("Lo", "St").Id, 200m, null, pool.Id, reserveCredit: true);
        await harness.Allocations.WithdrawAsync(lost.Id, reason.Id, creditReusable: false, releaseCashCommitment: false);

        var withCosts = await harness.Reports.GetWithdrawnStudentsAsync(withCosts: true);
        var withoutCosts = await harness.Reports.GetWithdrawnStudentsAsync(withCosts: false);

        Assert.Equal(new[] { lost.Id }, withCosts.Select(a => a.Id));
        Assert.Equal(new[] { reusable.Id }, withoutCosts.Select(a => a.Id));
    }

    [Fact]
    public async Task CreditPoolSummary_MatchesTheLedger()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync("Provider A");
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, pool.Id, reserveCredit: true);
        await harness.Allocations.MarkCompletedAsync(allocation.Id);
        await harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider A");

        var summary = await harness.Reports.GetCreditPoolSummaryAsync();

        var row = Assert.Single(summary);
        Assert.Equal(10m, row.Loaded);
        Assert.Equal(1m, row.Consumed);
        Assert.Equal(0m, row.Allocated);
        Assert.Equal(9m, row.Available);
    }

    [Fact]
    public async Task BudgetSummary_ReportsActualAndForecastSeparately()
    {
        using var harness = new TestHarness();
        var pool = await harness.BudgetPoolAsync("Training budget");
        await harness.Budgets.AddFundsAsync(pool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, pool.Id, createCashCommitment: true);

        var row = Assert.Single(await harness.Reports.GetBudgetSummaryAsync());

        Assert.Equal(1000m, row.ActualAvailable);
        Assert.Equal(800m, row.ForecastAvailable);
        Assert.Equal(200m, row.PendingCommitments);
        Assert.Single(await harness.Reports.GetPendingCommitmentsAsync());
        Assert.Equal(allocation.Id, (await harness.Reports.GetPendingCommitmentsAsync())[0].AllocationId);
    }

    [Fact]
    public async Task TbcDeliveries_IncludeUnconfirmedAndUndatedDeliveries()
    {
        using var harness = new TestHarness();
        var confirmed = harness.AddDelivery();
        confirmed.StartDate = new DateTime(2026, 3, 1);
        confirmed.DateStatus = DeliveryDateStatus.Confirmed;
        var tbc = harness.AddDelivery();
        tbc.DateStatus = DeliveryDateStatus.TBC;
        await harness.Context.SaveChangesAsync();

        var results = await harness.Reports.GetTbcDeliveriesAsync();

        Assert.Equal(new[] { tbc.Id }, results.Select(d => d.Id));
    }

    [Fact]
    public async Task BillableCertificates_ExcludeBatchesAlreadyExported()
    {
        using var harness = new TestHarness();
        var pool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, pool.Id, reserveCredit: true);
        await harness.Allocations.MarkCompletedAsync(allocation.Id);
        await harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider A");

        Assert.Single(await harness.Reports.GetBillableCertificatesAsync());

        allocation.ExportedInBatchId = Guid.NewGuid();
        await harness.Context.SaveChangesAsync();

        Assert.Empty(await harness.Reports.GetBillableCertificatesAsync());
        Assert.Single(await harness.Reports.GetBillableCertificatesAsync(includeExported: true));
    }

    [Fact]
    public async Task AuditActivity_RecordsTheKeyLifecycleEvents()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id);
        await harness.Allocations.MarkCompletedAsync(allocation.Id);

        var log = await harness.Reports.GetAuditActivityAsync(entityType: "Allocation");

        Assert.Contains(log, e => e.Action == "Created");
        Assert.Contains(log, e => e.Action == "Outcome");
    }
}
