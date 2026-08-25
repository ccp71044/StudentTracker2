using StudentTracker.Core.Enums;

namespace StudentTracker.Tests;

/// <summary>
/// Covers the cash budget rules in design section 10.2. Actual and forecast availability are
/// always reported separately, and a commitment that becomes an expense is only counted once.
/// </summary>
public class BudgetLedgerTests
{
    [Fact]
    public async Task Commitment_AffectsForecastOnlyNotActual()
    {
        using var harness = new TestHarness();
        var pool = await harness.BudgetPoolAsync();
        await harness.Budgets.AddFundsAsync(pool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id, 200m, pool.Id);

        await harness.Budgets.CreateCommitmentAsync(pool.Id, allocation.Id, 200m);

        var balance = await harness.Budgets.GetBalanceAsync(pool.Id);
        Assert.Equal(1000m, balance.ActualAvailable);
        Assert.Equal(800m, balance.ForecastAvailable);
        Assert.Equal(200m, balance.PendingCommitments);
    }

    /// <summary>
    /// Regression: recognising an expense used to leave the pending commitment in place, so the
    /// same money was subtracted from the forecast twice.
    /// </summary>
    [Fact]
    public async Task RecognisingAnExpense_RetiresThePendingCommitment()
    {
        using var harness = new TestHarness();
        var pool = await harness.BudgetPoolAsync();
        await harness.Budgets.AddFundsAsync(pool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id, 200m, pool.Id);
        await harness.Budgets.CreateCommitmentAsync(pool.Id, allocation.Id, 200m);

        await harness.Budgets.RecogniseExpenseAsync(pool.Id, allocation.Id, 200m);

        var balance = await harness.Budgets.GetBalanceAsync(pool.Id);
        Assert.Equal(200m, balance.ActualExpenditure);
        Assert.Equal(0m, balance.PendingCommitments);
        Assert.Equal(800m, balance.ActualAvailable);
        Assert.Equal(800m, balance.ForecastAvailable);
    }

    [Fact]
    public async Task ReleasingACommitment_RestoresTheForecast()
    {
        using var harness = new TestHarness();
        var pool = await harness.BudgetPoolAsync();
        await harness.Budgets.AddFundsAsync(pool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id, 200m, pool.Id);
        await harness.Budgets.CreateCommitmentAsync(pool.Id, allocation.Id, 200m);

        await harness.Budgets.ReleaseCommitmentAsync(pool.Id, allocation.Id, 200m, "Student withdrew");

        var balance = await harness.Budgets.GetBalanceAsync(pool.Id);
        Assert.Equal(0m, balance.PendingCommitments);
        Assert.Equal(1000m, balance.ForecastAvailable);
    }

    [Fact]
    public async Task ExpenseReversal_ReturnsMoneyToActualAvailable()
    {
        using var harness = new TestHarness();
        var pool = await harness.BudgetPoolAsync();
        await harness.Budgets.AddFundsAsync(pool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(delivery.Id, harness.AddStudent().Id, 200m, pool.Id);
        var expense = await harness.Budgets.RecogniseExpenseAsync(pool.Id, allocation.Id, 200m);

        await harness.Budgets.ReverseExpenseAsync(expense.Id, "Charged to the wrong pool");

        var balance = await harness.Budgets.GetBalanceAsync(pool.Id);
        Assert.Equal(0m, balance.ActualExpenditure);
        Assert.Equal(1000m, balance.ActualAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Budgets.ReverseExpenseAsync(expense.Id, "Again"));
    }

    [Fact]
    public async Task ConsumingCreditDoesNotChangeTheCashPosition()
    {
        using var harness = new TestHarness();
        var creditPool = await harness.CreditPoolAsync();
        var budgetPool = await harness.BudgetPoolAsync();
        await harness.Credits.TopUpAsync(creditPool.Id, 10m, 10m);
        await harness.Budgets.AddFundsAsync(budgetPool.Id, 1000m);
        var delivery = harness.AddDelivery(200m);
        var allocation = await harness.Allocations.AllocateStudentAsync(
            delivery.Id, harness.AddStudent().Id, 200m, budgetPool.Id, creditPool.Id, reserveCredit: true);

        await harness.Credits.ConsumeAsync(creditPool.Id, allocation.Id, 1m, 1, "Certificate ordered");

        var balance = await harness.Budgets.GetBalanceAsync(budgetPool.Id);
        Assert.Equal(1000m, balance.ActualAvailable);
        Assert.Equal(0m, balance.ActualExpenditure);
    }
}
