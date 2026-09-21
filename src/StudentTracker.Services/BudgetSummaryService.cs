using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Answers the three questions the register was being used to answer by hand: how much is left,
/// how much of it is already promised, and how many more students can be put through.
/// </summary>
public class BudgetSummaryService
{
    private readonly StudentTrackerDbContext _context;
    private readonly PricingService _pricing;

    public BudgetSummaryService(StudentTrackerDbContext context, PricingService pricing)
    {
        _context = context;
        _pricing = pricing;
    }

    public async Task<List<PoolSummary>> GetPoolSummariesAsync()
    {
        var pools = await _context.BudgetPools.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var transactions = await _context.BudgetTransactions.ToListAsync();
        var allocations = await _context.Allocations
            .Where(a => a.BudgetPoolId != null)
            .ToListAsync();

        return pools.Select(pool =>
        {
            var poolTransactions = transactions.Where(t => t.PoolId == pool.Id).ToList();
            var poolAllocations = allocations.Where(a => a.BudgetPoolId == pool.Id).ToList();
            var added = poolTransactions.Where(t => t.TransactionType == BudgetTransactionType.FundsAdded).Sum(t => t.Amount);
            var spent = -poolTransactions.Where(t => t.TransactionType == BudgetTransactionType.ExpenseRecognised || t.TransactionType == BudgetTransactionType.ExpenseReversed).Sum(t => t.Amount);
            var committed = -poolTransactions
                .Where(t => t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased)
                .Sum(t => t.Amount);
            var adjustments = poolTransactions
                .Where(t => t.TransactionType is BudgetTransactionType.Adjustment or BudgetTransactionType.Reimbursement or BudgetTransactionType.Reversal)
                .Sum(t => t.Amount);

            return new PoolSummary
            {
                PoolId = pool.Id,
                Name = pool.Name,
                FundsAdded = added + adjustments,
                Spent = spent,
                Committed = committed,
                UnassignedPlaceholderPlaces = poolAllocations.Count(a => a.StudentId == null && !string.IsNullOrWhiteSpace(a.PlaceholderName)),
                AssignedPendingPlaces = poolAllocations.Count(a => a.StudentId != null && a.OutcomeStatus == OutcomeStatus.Pending),
                CompletedAwaitingManualSpend = poolAllocations.Count(a =>
                    a.OutcomeStatus == OutcomeStatus.Completed &&
                    a.CashCommitmentStatus != CashCommitmentStatus.Spent)
            };
        }).ToList();
    }

    /// <summary>
    /// How many more completions of each course the free balance covers. Courses without a price
    /// are excluded, since a completion count cannot be derived without one.
    /// </summary>
    public async Task<List<CompletionsRemaining>> GetCompletionsRemainingAsync(Guid? poolId = null)
    {
        var summaries = await GetPoolSummariesAsync();
        if (poolId.HasValue)
            summaries = summaries.Where(s => s.PoolId == poolId.Value).ToList();

        var prices = await _pricing.GetCurrentPricesAsync();
        var courses = await _context.CourseDefinitions
            .Where(c => c.IsActive && prices.Keys.Contains(c.Id))
            .ToListAsync();

        return summaries
            .SelectMany(pool => courses
                .Where(c => prices[c.Id] > 0)
                .Select(c => new CompletionsRemaining
                {
                    PoolId = pool.PoolId,
                    PoolName = pool.Name,
                    CourseDefinitionId = c.Id,
                    CourseCode = c.CourseCode,
                    CourseTitle = c.CourseTitle,
                    CompletionPrice = prices[c.Id],
                    Remaining = pool.Free <= 0 ? 0 : (int)Math.Floor(pool.Free / prices[c.Id])
                }))
            .OrderBy(c => c.PoolName)
            .ThenBy(c => c.CourseCode)
            .ToList();
    }

    /// <summary>
    /// Compares the register's recorded top-ups against the provider's credit purchases. Anything
    /// that does not line up on date and amount is reported rather than silently corrected.
    /// </summary>
    public async Task<ReconciliationResult> ReconcileTopUpsAsync()
    {
        var registerTopUps = await _context.BudgetTransactions
            .Where(t => t.TransactionType == BudgetTransactionType.FundsAdded)
            .ToListAsync();

        var providerTopUps = await _context.CertificateCreditTransactions
            .Where(t => t.TransactionType == CreditTransactionType.TopUp && t.SourceType == CreditSourceType.ProviderHistory)
            .ToListAsync();

        var unmatchedProvider = providerTopUps.ToList();
        var result = new ReconciliationResult
        {
            RegisterTotal = registerTopUps.Sum(t => t.Amount),
            ProviderTotal = providerTopUps.Sum(t => t.Amount)
        };

        foreach (var register in registerTopUps.OrderBy(t => t.TransactionDate))
        {
            var exact = unmatchedProvider.FirstOrDefault(p =>
                p.Amount == register.Amount && WithinDays(p.TransactionDateTime, register.TransactionDate, 3));

            if (exact != null)
            {
                unmatchedProvider.Remove(exact);
                continue;
            }

            // A near match on the same day is almost always the same payment recorded to the dollar.
            var near = unmatchedProvider.FirstOrDefault(p =>
                Math.Abs(p.Amount - register.Amount) <= 1m && WithinDays(p.TransactionDateTime, register.TransactionDate, 3));

            if (near != null)
            {
                unmatchedProvider.Remove(near);
                result.Discrepancies.Add(new ReconciliationDiscrepancy
                {
                    Date = register.TransactionDate,
                    RegisterAmount = register.Amount,
                    ProviderAmount = near.Amount,
                    Issue = string.Format(Money, "Register records {0:C} but the provider records {1:C}.", register.Amount, near.Amount)
                });
                continue;
            }

            result.Discrepancies.Add(new ReconciliationDiscrepancy
            {
                Date = register.TransactionDate,
                RegisterAmount = register.Amount,
                ProviderAmount = null,
                Issue = string.Format(Money, "Register top-up of {0:C} has no matching provider purchase.", register.Amount)
            });
        }

        foreach (var provider in unmatchedProvider)
        {
            result.Discrepancies.Add(new ReconciliationDiscrepancy
            {
                Date = provider.TransactionDateTime,
                RegisterAmount = null,
                ProviderAmount = provider.Amount,
                Issue = string.Format(Money, "Provider purchase of {0:C} is not recorded in the register.", provider.Amount)
            });
        }

        return result;
    }

    /// <summary>Amounts are always Australian dollars, regardless of the machine's locale.</summary>
    private static readonly CultureInfo Money = new("en-AU");

    private static bool WithinDays(DateTime left, DateTime right, int days) =>
        Math.Abs((left.Date - right.Date).TotalDays) <= days;
}

public class PoolSummary
{
    public Guid PoolId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal FundsAdded { get; init; }
    public decimal Spent { get; init; }
    public decimal Committed { get; init; }
    public decimal Balance => FundsAdded - Spent;
    public decimal Free => Balance - Committed;
    public decimal Available => Free;
    public int UnassignedPlaceholderPlaces { get; init; }
    public int AssignedPendingPlaces { get; init; }
    public int CompletedAwaitingManualSpend { get; init; }
}

public class CompletionsRemaining
{
    public Guid PoolId { get; init; }
    public string PoolName { get; init; } = string.Empty;
    public Guid CourseDefinitionId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseTitle { get; init; } = string.Empty;
    public decimal CompletionPrice { get; init; }
    public int Remaining { get; init; }
}

public class ReconciliationResult
{
    public decimal RegisterTotal { get; init; }
    public decimal ProviderTotal { get; init; }
    public decimal Difference => ProviderTotal - RegisterTotal;
    public List<ReconciliationDiscrepancy> Discrepancies { get; } = new();
    public bool IsBalanced => Discrepancies.Count == 0;
}

public class ReconciliationDiscrepancy
{
    public DateTime Date { get; init; }
    public decimal? RegisterAmount { get; init; }
    public decimal? ProviderAmount { get; init; }
    public string Issue { get; init; } = string.Empty;
}
