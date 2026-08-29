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

    public async Task<List<BudgetPool>> GetPoolsAsync(bool includeInactive = false) => await _context.BudgetPools.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync();

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

    public async Task ArchivePoolAsync(Guid id) => await SetPoolActiveAsync(id, false);

    public async Task RestorePoolAsync(Guid id) => await SetPoolActiveAsync(id, true);

    private async Task SetPoolActiveAsync(Guid id, bool active)
    {
        var pool = await _context.BudgetPools.FindAsync(id) ?? throw new ArgumentException("Budget pool not found");
        if (!active)
        {
            var activeAllocations = await _context.Allocations.CountAsync(a => a.BudgetPoolId == id && a.CashCommitmentStatus == CashCommitmentStatus.Pending);
            if (activeAllocations > 0)
            {
                _audit.Record("ArchiveBlocked", "BudgetPool", pool.Id, pool.DisplayId, null, new { PendingCommitments = activeAllocations });
                await _context.SaveChangesAsync();
                throw new InvalidOperationException($"Budget pool has {activeAllocations} pending commitment(s). Release or recognise them before archiving.");
            }
        }
        pool.IsActive = active;
        pool.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record(active ? "Restored" : "Archived", "BudgetPool", pool.Id, pool.DisplayId);
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
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.None && allocation.CashCommitmentStatus != CashCommitmentStatus.Released)
            throw new InvalidOperationException("Commitment can only be created when the allocation has no active commitment.");

        var forecast = await GetForecastAvailableAsync(poolId);
        if (forecast < amount)
        {
            _audit.Record("CommitmentBlocked", "Allocation", allocation.Id, allocation.DisplayId, null, new { Requested = amount, Available = forecast });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException($"Insufficient budget funds. Available: {forecast:C}, requested: {amount:C}.");
        }

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
        allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentCreated", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = amount, PoolId = poolId });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<BudgetTransaction> ReleaseCommitmentAsync(Guid poolId, Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Pending)
            throw new InvalidOperationException("Only a pending commitment can be released.");

        var committed = await GetAllocationCommitmentAsync(allocationId);
        if (committed <= 0)
            throw new InvalidOperationException("No outstanding commitment amount to release.");

        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.CommitmentReleased,
            Amount = committed,
            Reason = reason ?? "Commitment released",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentReleased", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<BudgetTransaction> RecogniseExpenseAsync(Guid poolId, Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Pending)
            throw new InvalidOperationException("Expense can only be recognised when a commitment is pending.");

        var committed = await GetAllocationCommitmentAsync(allocationId);
        if (committed <= 0)
            throw new InvalidOperationException("No outstanding commitment amount to recognise.");

        var releaseTx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.CommitmentReleased,
            Amount = committed,
            Reason = reason ?? "Commitment released for expense recognition",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(releaseTx);

        var expenseTx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.ExpenseRecognised,
            Amount = -committed,
            Reason = reason ?? "Expense recognised",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(expenseTx);

        allocation.CashCommitmentStatus = CashCommitmentStatus.Spent;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseRecognised", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = committed, PoolId = poolId });
        await _context.SaveChangesAsync();
        return expenseTx;
    }

    public async Task<BudgetTransaction> ReverseExpenseAsync(Guid poolId, Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Spent)
            throw new InvalidOperationException("Only a spent cost can be reversed.");

        var expense = await GetAllocationExpenseAsync(allocationId);
        if (expense <= 0)
            throw new InvalidOperationException("No recognised expense to reverse.");

        var tx = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = BudgetTransactionType.ExpenseReversed,
            Amount = expense,
            Reason = reason ?? "Spent cost reversed",
            TransactionDate = DateTime.UtcNow
        };
        _context.BudgetTransactions.Add(tx);
        allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseReversed", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = expense, PoolId = poolId });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<decimal> GetFundsAddedAsync(Guid poolId) =>
        await _context.BudgetTransactions.Where(t => t.PoolId == poolId && t.TransactionType == BudgetTransactionType.FundsAdded).SumAsync(t => t.Amount);

    public async Task<decimal> GetActualExpenditureAsync(Guid poolId) =>
        -await _context.BudgetTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == BudgetTransactionType.ExpenseRecognised || t.TransactionType == BudgetTransactionType.ExpenseReversed))
            .SumAsync(t => t.Amount);

    public async Task<decimal> GetPendingCommitmentsAsync(Guid poolId) =>
        -await _context.BudgetTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased))
            .SumAsync(t => t.Amount);

    public async Task<decimal> GetActualAvailableAsync(Guid poolId) => await GetFundsAddedAsync(poolId) - await GetActualExpenditureAsync(poolId);
    public async Task<decimal> GetForecastAvailableAsync(Guid poolId) => await GetActualAvailableAsync(poolId) - await GetPendingCommitmentsAsync(poolId);

    public async Task<decimal> GetAllocationCommitmentAsync(Guid allocationId) =>
        -await _context.BudgetTransactions
            .Where(t => t.AllocationId == allocationId && (t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased))
            .SumAsync(t => t.Amount);

    public async Task<decimal> GetAllocationExpenseAsync(Guid allocationId) =>
        -await _context.BudgetTransactions
            .Where(t => t.AllocationId == allocationId && (t.TransactionType == BudgetTransactionType.ExpenseRecognised || t.TransactionType == BudgetTransactionType.ExpenseReversed))
            .SumAsync(t => t.Amount);

    public async Task<List<BudgetTransaction>> GetTransactionsAsync(Guid poolId) =>
        await _context.BudgetTransactions.Where(t => t.PoolId == poolId).OrderByDescending(t => t.TransactionDate).ToListAsync();
}
