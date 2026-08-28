namespace StudentTracker.Core.Models;

/// <summary>
/// Transaction-derived balance for a cash budget pool. Actual and forecast figures are always
/// reported separately so that no screen shows an ambiguous "remaining" value.
/// </summary>
public record BudgetPoolBalance(
    decimal FundsAdded,
    decimal ActualExpenditure,
    decimal PendingCommitments)
{
    public decimal ActualAvailable => FundsAdded - ActualExpenditure;

    public decimal ForecastAvailable => ActualAvailable - PendingCommitments;

    public static BudgetPoolBalance Empty { get; } = new(0m, 0m, 0m);
}
