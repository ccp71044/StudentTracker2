using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;

namespace StudentTracker.Tests;

/// <summary>
/// End-to-end service workflows from design section 12 and the WF-001..WF-010 scenarios.
/// </summary>
public class WorkflowTests
{
    /// <summary>WF-001: allocate, complete, order, deliver.</summary>
    [Fact]
    public async Task StandardLifecycle_LeavesConsistentCreditAndCashPositions()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        var budgetPool = await harness.BudgetPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        await harness.Budgets.AddFundsAsync(budgetPool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);

        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, budgetPool.Id, creditPool.Id,
            reserveCredit: true, createCashCommitment: true);

        Assert.Equal(CreditStatus.Allocated, allocation.CreditStatus);
        Assert.Equal(CashCommitmentStatus.Pending, allocation.CashCommitmentStatus);
        Assert.Equal(200m, (await harness.Budgets.GetBalanceAsync(budgetPool.Id)).PendingCommitments);

        await harness.Allocations.MarkCompletedAsync(allocation.Id, DateTime.UtcNow);

        var afterCompletion = await harness.Budgets.GetBalanceAsync(budgetPool.Id);
        Assert.Equal(200m, afterCompletion.ActualExpenditure);
        Assert.Equal(0m, afterCompletion.PendingCommitments);
        Assert.Equal(800m, afterCompletion.ForecastAvailable);

        var order = await harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider Ltd");
        var creditBalance = await harness.Credits.GetBalanceAsync(creditPool.Id);
        Assert.Equal(1m, creditBalance.Consumed);
        Assert.Equal(0m, creditBalance.Allocated);
        Assert.Equal(9m, creditBalance.Available);

        await harness.Certificates.RecordDeliveryAsync(order.Id, DateTime.UtcNow, "Email", "Site manager");

        var reloaded = await harness.Context.Allocations.FindAsync(allocation.Id);
        Assert.Equal(CertificateDeliveryStatus.Delivered, reloaded!.CertificateDeliveryStatus);
        Assert.Equal(800m, (await harness.Budgets.GetBalanceAsync(budgetPool.Id)).ActualAvailable);
    }

    /// <summary>WF-002: allocation and its ledger entries are written as one unit.</summary>
    [Fact]
    public async Task AllocationRollsBackWhenCreditReservationFails()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        var delivery = harness.AddDelivery(500m);
        var student = harness.AddStudent();

        // The pool has no credit at all, so reserving a certificate must fail.
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Allocations.AllocateStudentAsync(
            delivery.Id, student.Id, 500m, null, creditPool.Id, reserveCredit: true));

        Assert.Empty(await harness.Context.Allocations.ToListAsync());
        Assert.Equal(0m, (await harness.Credits.GetBalanceAsync(creditPool.Id)).Available);
    }

    [Fact]
    public async Task ReservingCreditWithoutAPoolIsRejectedAndCreatesNothing()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 100m, reserveCredit: true));

        Assert.Empty(await harness.Context.Allocations.ToListAsync());
    }

    /// <summary>WF-003: withdrawal where the credit can be reused.</summary>
    [Fact]
    public async Task WithdrawalWithReusableCredit_ReleasesCreditAndCash()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        var budgetPool = await harness.BudgetPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        await harness.Budgets.AddFundsAsync(budgetPool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, budgetPool.Id, creditPool.Id, true, true);
        var reason = harness.AddReason("Withdrawal", "Left the company");

        var withdrawn = await harness.Allocations.WithdrawAsync(allocation.Id, reason.Id, creditReusable: true, releaseCashCommitment: true);

        Assert.Equal(OutcomeStatus.Withdrawn, withdrawn.OutcomeStatus);
        Assert.Equal(CertificateOrderStatus.NotRequired, withdrawn.CertificateOrderStatus);
        var credit = await harness.Credits.GetBalanceAsync(creditPool.Id);
        Assert.Equal(10m, credit.Available);
        Assert.Equal(0m, credit.Unavailable);
        Assert.Equal(1000m, (await harness.Budgets.GetBalanceAsync(budgetPool.Id)).ForecastAvailable);
    }

    /// <summary>WF-004: late withdrawal where the credit is lost rather than spent.</summary>
    [Fact]
    public async Task WithdrawalWithNonReusableCredit_RecordsACreditLoss()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, creditPool.Id, reserveCredit: true);
        var reason = harness.AddReason("Withdrawal", "Withdrew inside notice period");

        await harness.Allocations.WithdrawAsync(allocation.Id, reason.Id, creditReusable: false, releaseCashCommitment: false);

        var credit = await harness.Credits.GetBalanceAsync(creditPool.Id);
        Assert.Equal(1m, credit.Unavailable);
        Assert.Equal(0m, credit.Consumed);
        Assert.Equal(9m, credit.Available);
    }

    [Fact]
    public async Task WithdrawalRequiresNotesWhenTheReasonDemandsThem()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id);
        var reason = harness.AddReason("Withdrawal", "Other", requiresNotes: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Allocations.WithdrawAsync(allocation.Id, reason.Id, true, true));

        var reloaded = await harness.Context.Allocations.FindAsync(allocation.Id);
        Assert.Equal(OutcomeStatus.Pending, reloaded!.OutcomeStatus);
    }

    /// <summary>WF-005: non-completion is distinct from withdrawal and may still consume credit.</summary>
    [Fact]
    public async Task NonCompletion_ConsumesCreditWithoutBecomingAWithdrawal()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, creditPool.Id, reserveCredit: true);
        var reason = harness.AddReason("NonCompletion", "Did not pass assessment");

        var result = await harness.Allocations.MarkNonCompletedAsync(allocation.Id, reason.Id, consumeCredit: true);

        Assert.Equal(OutcomeStatus.NotCompleted, result.OutcomeStatus);
        var credit = await harness.Credits.GetBalanceAsync(creditPool.Id);
        Assert.Equal(1m, credit.Consumed);
        Assert.Equal(0m, credit.Unavailable);
    }

    /// <summary>WF-006: a certificate cannot be ordered for a student who has not completed.</summary>
    [Fact]
    public async Task OrderingACertificateRequiresCompletion()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, creditPool.Id, reserveCredit: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider Ltd"));
    }

    /// <summary>WF-007: duplicate ordinary orders are blocked; replacements need a reason.</summary>
    [Fact]
    public async Task DuplicateOrderIsBlockedAndReplacementRequiresAReason()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 1000m, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, null, creditPool.Id, reserveCredit: true);
        await harness.Allocations.MarkCompletedAsync(allocation.Id);
        await harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider Ltd");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider Ltd"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Certificates.OrderCertificateAsync(allocation.Id, "Provider Ltd", isReplacement: true));
    }

    /// <summary>WF-008: a placeholder holds a seat and is replaced by a real student later.</summary>
    [Fact]
    public async Task PlaceholderCanBeReplacedByARealStudent()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();
        var placeholder = await harness.Allocations.CreatePlaceholderAsync(delivery.Id, "SCJV PENDING");
        var student = harness.AddStudent("Real", "Person");

        var filled = await harness.Allocations.ReplacePlaceholderAsync(placeholder.Id, student.Id);

        Assert.Equal(student.Id, filled.StudentId);
        Assert.Null(filled.PlaceholderName);
        Assert.Single(await harness.Context.Allocations.ToListAsync());
    }

    [Fact]
    public async Task StudentCannotBeAllocatedTwiceToTheSameDelivery()
    {
        using var harness = new TestHarness();
        var delivery = harness.AddDelivery();
        var student = harness.AddStudent();
        await harness.Allocations.AllocateStudentAsync(delivery.Id, student.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Allocations.AllocateStudentAsync(delivery.Id, student.Id));
    }
}
