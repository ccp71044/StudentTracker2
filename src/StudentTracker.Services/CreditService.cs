using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class CreditService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public CreditService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<List<CertificateCreditPool>> GetPoolsAsync() => await _context.CertificateCreditPools.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

    public async Task<CertificateCreditPool?> GetPoolAsync(Guid id) => await _context.CertificateCreditPools.FindAsync(id);

    public async Task<CertificateCreditPool> CreatePoolAsync(CertificateCreditPool pool)
    {
        pool.DisplayId = _idGenerator.NextDisplayId<CertificateCreditPool>("CRP");
        _context.CertificateCreditPools.Add(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "CertificateCreditPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task<CertificateCreditTransaction> TopUpAsync(Guid poolId, decimal amount, decimal? quantity = null, string? reason = null, string? externalRef = null, CreditSourceType sourceType = CreditSourceType.Manual)
    {
        var tx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = poolId,
            TransactionType = CreditTransactionType.TopUp,
            Amount = amount,
            Quantity = quantity,
            SourceType = sourceType,
            ExternalPurchaseReference = externalRef,
            Reason = reason ?? "Top-up",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(tx);
        await _context.SaveChangesAsync();
        _audit.Record("TopUp", "CertificateCreditTransaction", tx.Id, tx.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> AllocateAsync(Guid poolId, Guid allocationId, decimal amount, decimal? quantity = null, string? reason = null)
    {
        var available = await GetAvailableAsync(poolId);
        if (amount > available)
            throw new InvalidOperationException($"Insufficient credit available. Available: {available}, requested: {amount}");

        var tx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = CreditTransactionType.Allocate,
            Amount = amount,
            Quantity = quantity,
            Reason = reason ?? "Allocated to student",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditStatus = CreditStatus.Allocated;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditAllocated", "Allocation", allocation.Id, allocation.DisplayId, null, new { CreditTransactionId = tx.DisplayId });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> ConsumeAsync(Guid poolId, Guid allocationId, decimal amount, decimal? quantity = null, string? reason = null, CreditTransactionType type = CreditTransactionType.OrderConsume)
    {
        var tx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = type,
            Amount = amount,
            Quantity = quantity,
            Reason = reason ?? "Certificate ordered",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditStatus = CreditStatus.Consumed;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditConsumed", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> ReleaseAsync(Guid poolId, Guid allocationId, decimal amount, string? reason = null)
    {
        var tx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = CreditTransactionType.Release,
            Amount = -amount,
            Reason = reason ?? "Credit released",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditStatus = CreditStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditReleased", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<(CertificateCreditTransaction Out, CertificateCreditTransaction In)> ReallocateAsync(Guid sourcePoolId, Guid targetPoolId, Guid sourceAllocationId, Guid targetAllocationId, decimal amount, string? reason = null)
    {
        var outTx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = sourcePoolId,
            AllocationId = sourceAllocationId,
            TransactionType = CreditTransactionType.ReallocateOut,
            Amount = -amount,
            Reason = reason ?? "Reallocated out",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(outTx);
        await _context.SaveChangesAsync();

        var inTx = new CertificateCreditTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = targetPoolId,
            AllocationId = targetAllocationId,
            LinkedTransactionId = outTx.Id,
            TransactionType = CreditTransactionType.ReallocateIn,
            Amount = amount,
            Reason = reason ?? "Reallocated in",
            TransactionDateTime = DateTime.UtcNow
        };
        _context.CertificateCreditTransactions.Add(inTx);

        outTx.LinkedTransactionId = inTx.Id;

        var sourceAllocation = await _context.Allocations.FindAsync(sourceAllocationId) ?? throw new ArgumentException("Source allocation not found");
        sourceAllocation.CreditStatus = CreditStatus.Reallocated;
        sourceAllocation.UpdatedAt = DateTime.UtcNow;

        var targetAllocation = await _context.Allocations.FindAsync(targetAllocationId) ?? throw new ArgumentException("Target allocation not found");
        targetAllocation.CreditStatus = CreditStatus.Allocated;
        targetAllocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditReallocated", "Allocation", sourceAllocation.Id, sourceAllocation.DisplayId, null, new { TargetAllocationId = targetAllocation.DisplayId });
        await _context.SaveChangesAsync();
        return (outTx, inTx);
    }

    public async Task<decimal> GetLoadedAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == CreditTransactionType.TopUp || t.TransactionType == CreditTransactionType.Adjustment))
            .SumAsync(t => t.TransactionType == CreditTransactionType.Adjustment ? t.Amount : Math.Abs(t.Amount));

    public async Task<decimal> GetAllocatedAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == CreditTransactionType.Allocate || t.TransactionType == CreditTransactionType.Reserve))
            .SumAsync(t => t.Amount);

    public async Task<decimal> GetConsumedAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId && (t.TransactionType == CreditTransactionType.OrderConsume || t.TransactionType == CreditTransactionType.ManualConsume))
            .SumAsync(t => Math.Abs(t.Amount));

    public async Task<decimal> GetExpiredAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId && t.TransactionType == CreditTransactionType.Expire)
            .SumAsync(t => Math.Abs(t.Amount));

    public async Task<decimal> GetAvailableAsync(Guid poolId)
    {
        var loaded = await GetLoadedAsync(poolId);
        var allocated = await GetAllocatedAsync(poolId);
        var consumed = await GetConsumedAsync(poolId);
        var expired = await GetExpiredAsync(poolId);
        return loaded - allocated - consumed - expired;
    }

    public async Task<List<CertificateCreditTransaction>> GetTransactionsAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId)
            .OrderByDescending(t => t.TransactionDateTime)
            .Include(t => t.Allocation).ThenInclude(a => a!.Student)
            .ToListAsync();
}
