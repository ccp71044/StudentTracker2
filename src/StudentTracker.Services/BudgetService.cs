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

    public async Task<BudgetTransaction> RecogniseExpenseAsync(Guid poolId, Guid allocationId, decimal amount, string? reason = null)
    {
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
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CashCommitmentStatus = CashCommitmentStatus.Spent;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseRecognised", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<decimal> GetFundsAddedAsync(Guid poolId) =>
        await _context.BudgetTransactions.Where(t => t.PoolId == poolId && t.TransactionType == BudgetTransactionType.FundsAdded).SumAsync(t => t.Amount);

    public async Task<decimal> GetActualExpenditureAsync(Guid poolId) =>
        -await _context.BudgetTransactions.Where(t => t.PoolId == poolId && t.TransactionType == BudgetTransactionType.ExpenseRecognised).SumAsync(t => t.Amount);

    public async Task<decimal> GetPendingCommitmentsAsync(Guid poolId) =>
        -await _context.BudgetTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased))
            .SumAsync(t => t.Amount);

    public async Task<decimal> GetActualAvailableAsync(Guid poolId) => await GetFundsAddedAsync(poolId) - await GetActualExpenditureAsync(poolId);
    public async Task<decimal> GetForecastAvailableAsync(Guid poolId) => await GetActualAvailableAsync(poolId) - await GetPendingCommitmentsAsync(poolId);

    public async Task<List<BudgetTransaction>> GetTransactionsAsync(Guid poolId) =>
        await _context.BudgetTransactions.Where(t => t.PoolId == poolId).OrderByDescending(t => t.TransactionDate).ToListAsync();
}
