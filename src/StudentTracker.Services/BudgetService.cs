using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class BudgetService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public BudgetService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<List<BudgetPool>> GetPoolsAsync() => await _context.BudgetPools.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

    public async Task<BudgetPool?> GetPoolAsync(Guid id) => await _context.BudgetPools.FindAsync(id);

    public async Task<BudgetPool> CreatePoolAsync(BudgetPool pool)
    {
        pool.DisplayId = _idGenerator.NextDisplayId<BudgetPool>("BUD");
        _context.BudgetPools.Add(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "BudgetPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task<BudgetPool> UpdatePoolAsync(BudgetPool pool)
    {
        pool.UpdatedAt = DateTime.UtcNow;
        _context.BudgetPools.Update(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "BudgetPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task ArchivePoolAsync(Guid id)
    {
        var pool = await _context.BudgetPools.FindAsync(id) ?? throw new ArgumentException("Budget pool not found");
        pool.IsActive = false;
        pool.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Archived", "BudgetPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task<BudgetTransaction> AddFundsAsync(Guid poolId, decimal amount, Guid? fundingSourceId = null, string? reason = null)
    {
        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = amount,
            FundingSourceId = fundingSourceId,
            Reason = reason ?? "Funds added",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        await _context.SaveChangesAsync();
        _audit.Record("FundsAdded", "BudgetTransaction", tx.Id, tx.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<BudgetTransaction> CreateCommitmentAsync(Guid poolId, Guid allocationId, decimal amount, string? reason = null)
    {
        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.CommitmentCreated,
            Amount = -amount,
            Reason = reason ?? "Cash commitment",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentCreated", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<BudgetTransaction> ReleaseCommitmentAsync(Guid poolId, Guid allocationId, decimal amount, string? reason = null)
    {
        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.CommitmentReleased,
            Amount = amount,
            Reason = reason ?? "Commitment released",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentReleased", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    /// <summary>
    /// Converts a pending commitment into actual expenditure. Any outstanding commitment for the
    /// allocation is retired first so that the amount is not subtracted from forecast twice
    /// (design section 10.2).
    /// </summary>
    public async Task<BudgetTransaction> RecogniseExpenseAsync(Guid poolId, Guid allocationId, decimal amount, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");

        var outstandingCommitment = await GetOutstandingCommitmentAsync(poolId, allocationId);
        if (outstandingCommitment > 0m)
        {
            _context.BudgetTransactions.Add(new BudgetTransaction
            {
                DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
                PoolId = poolId,
                AllocationId = allocationId,
                TransactionType = BudgetTransactionType.CommitmentReleased,
                Amount = outstandingCommitment,
                Reason = "Commitment converted to actual expenditure",
                TransactionDate = DateTime.UtcNow
            });
        }

        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.ExpenseRecognised,
            Amount = -Math.Abs(amount),
            Reason = reason ?? "Expense recognised",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        allocation.CashCommitmentStatus = CashCommitmentStatus.Spent;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseRecognised", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    /// <summary>
    /// Reverses a previously recognised expense, returning the money to actual available.
    /// </summary>
    public async Task<BudgetTransaction> ReverseExpenseAsync(Guid budgetTransactionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to reverse an expense.", nameof(reason));

        var original = await _context.BudgetTransactions.FindAsync(budgetTransactionId)
            ?? throw new ArgumentException("Budget transaction not found");
        if (original.TransactionType != BudgetTransactionType.ExpenseRecognised)
            throw new InvalidOperationException("Only recognised expenses can be reversed.");

        var alreadyReversed = await _context.BudgetTransactions
            .AnyAsync(t => t.TransactionType == BudgetTransactionType.Reversal && t.ReversesTransactionId == original.Id);
        if (alreadyReversed)
            throw new InvalidOperationException($"Transaction {original.DisplayId} has already been reversed.");

        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = original.PoolId,
            AllocationId = original.AllocationId,
            TransactionType = BudgetTransactionType.Reversal,
            Amount = -original.Amount,
            Reason = reason,
            ReversesTransactionId = original.Id,
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);

        if (original.AllocationId.HasValue)
        {
            var allocation = await _context.Allocations.FindAsync(original.AllocationId.Value);
            if (allocation is not null)
            {
                allocation.CashCommitmentStatus = CashCommitmentStatus.None;
                allocation.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        _audit.Record("ExpenseReversed", "BudgetTransaction", original.Id, original.DisplayId, null, new { Reason = reason });
        await _context.SaveChangesAsync();
        return tx;
    }

    /// <summary>
    /// The still-pending commitment for a single allocation.
    /// </summary>
    public async Task<decimal> GetOutstandingCommitmentAsync(Guid poolId, Guid allocationId)
    {
        // Summed in memory: SQLite cannot aggregate decimal server-side.
        var amounts = await _context.BudgetTransactions
            .Where(t => t.PoolId == poolId && t.AllocationId == allocationId
                        && (t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased))
            .Select(t => t.Amount)
            .ToListAsync();
        return Math.Max(0m, -amounts.Sum());
    }

    /// <summary>
    /// Calculates the pool balance from its transactions (design section 10.2).
    /// </summary>
    public async Task<BudgetPoolBalance> GetBalanceAsync(Guid poolId)
    {
        var rows = await _context.BudgetTransactions
            .Where(t => t.PoolId == poolId)
            .Select(t => new { t.TransactionType, t.Amount })
            .ToListAsync();

        decimal fundsAdded = 0m, expenditure = 0m, commitments = 0m;
        foreach (var row in rows)
        {
            switch (row.TransactionType)
            {
                case BudgetTransactionType.FundsAdded:
                case BudgetTransactionType.Reimbursement:
                    fundsAdded += Math.Abs(row.Amount);
                    break;
                case BudgetTransactionType.Adjustment:
                    fundsAdded += row.Amount;
                    break;
                case BudgetTransactionType.ExpenseRecognised:
                    expenditure += Math.Abs(row.Amount);
                    break;
                case BudgetTransactionType.Reversal:
                    expenditure -= Math.Abs(row.Amount);
                    break;
                case BudgetTransactionType.CommitmentCreated:
                    commitments += Math.Abs(row.Amount);
                    break;
                case BudgetTransactionType.CommitmentReleased:
                    commitments -= Math.Abs(row.Amount);
                    break;
            }
        }

        return new BudgetPoolBalance(fundsAdded, expenditure, Math.Max(0m, commitments));
    }

    public async Task<decimal> GetFundsAddedAsync(Guid poolId) => (await GetBalanceAsync(poolId)).FundsAdded;
    public async Task<decimal> GetActualExpenditureAsync(Guid poolId) => (await GetBalanceAsync(poolId)).ActualExpenditure;
    public async Task<decimal> GetPendingCommitmentsAsync(Guid poolId) => (await GetBalanceAsync(poolId)).PendingCommitments;
    public async Task<decimal> GetActualAvailableAsync(Guid poolId) => (await GetBalanceAsync(poolId)).ActualAvailable;
    public async Task<decimal> GetForecastAvailableAsync(Guid poolId) => (await GetBalanceAsync(poolId)).ForecastAvailable;

    public async Task<List<BudgetTransaction>> GetTransactionsAsync(Guid poolId) =>
        await _context.BudgetTransactions.Where(t => t.PoolId == poolId).OrderByDescending(t => t.TransactionDate).ToListAsync();
}
