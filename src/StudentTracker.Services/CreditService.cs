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
    private readonly IDocumentService _documentService;

    public CreditService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit, IDocumentService documentService)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
        _documentService = documentService;
    }

    public async Task<List<CertificateCreditPool>> GetPoolsAsync(bool includeInactive = false) => await _context.CertificateCreditPools.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync();

    public async Task<CertificateCreditPool?> GetPoolAsync(Guid id) => await _context.CertificateCreditPools.FindAsync(id);

    public async Task<CertificateCreditPool> UpdatePoolAsync(CertificateCreditPool pool)
    {
        pool.UpdatedAt = DateTime.UtcNow;
        _context.CertificateCreditPools.Update(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "CertificateCreditPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task ArchivePoolAsync(Guid poolId) => await SetPoolActiveAsync(poolId, false);

    public async Task RestorePoolAsync(Guid poolId) => await SetPoolActiveAsync(poolId, true);

    private async Task SetPoolActiveAsync(Guid poolId, bool active)
    {
        var pool = await _context.CertificateCreditPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");
        if (!active)
        {
            var allocated = await _context.Allocations.CountAsync(a => a.CreditPoolId == poolId && a.CreditStatus == CreditStatus.Allocated);
            if (allocated > 0)
            {
                _audit.Record("ArchiveBlocked", "CertificateCreditPool", pool.Id, pool.DisplayId, null, new { AllocatedCredits = allocated });
                await _context.SaveChangesAsync();
                throw new InvalidOperationException($"Credit pool has {allocated} active credit allocation(s). Release or consume them before archiving.");
            }
        }
        pool.IsActive = active;
        pool.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record(active ? "Restored" : "Archived", "CertificateCreditPool", pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
    }

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

    public async Task<CertificateCreditTransaction> TopUpWithReceiptAsync(
        Guid poolId,
        decimal amount,
        decimal? quantity = null,
        DateTime? transactionDate = null,
        string? reference = null,
        string? reason = null,
        string? notes = null,
        string? receiptFilePath = null)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (quantity.HasValue && quantity.Value <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        var pool = await _context.CertificateCreditPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        Document? document = null;
        string? copiedFilePath = null;

        try
        {
            var tx = new CertificateCreditTransaction
            {
                DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
                PoolId = poolId,
                TransactionType = CreditTransactionType.TopUp,
                Amount = amount,
                Quantity = quantity,
                SourceType = CreditSourceType.ProviderHistory,
                ExternalPurchaseReference = reference,
                Reason = reason ?? "Provider receipt top-up",
                Notes = notes,
                TransactionDateTime = transactionDate?.ToUniversalTime() ?? DateTime.UtcNow
            };
            _context.CertificateCreditTransactions.Add(tx);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(receiptFilePath))
            {
                document = await _documentService.AddDocumentAsync(
                    receiptFilePath,
                    "CreditReceipts",
                    displayName: Path.GetFileName(receiptFilePath),
                    description: $"Receipt for credit top-up {tx.DisplayId} on pool {pool.DisplayId}",
                    receivedDate: transactionDate);
                copiedFilePath = _documentService.GetFullPath(document);

                await _documentService.LinkDocumentAsync(document.Id, nameof(CertificateCreditTransaction), tx.Id, "Receipt");
                await _documentService.LinkDocumentAsync(document.Id, nameof(CertificateCreditPool), poolId, "PoolReceipt");
            }

            await _context.SaveChangesAsync();
            _audit.Record(
                "TopUpWithReceipt",
                nameof(CertificateCreditTransaction),
                tx.Id,
                tx.DisplayId,
                null,
                new { PoolId = pool.Id, PoolDisplayId = pool.DisplayId, Amount = amount, Quantity = quantity, DocumentId = document?.Id, DocumentDisplayId = document?.DisplayId });
            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();
            return tx;
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            CleanupCopiedReceipt(copiedFilePath);
            throw;
        }
    }

    private static void CleanupCopiedReceipt(string? copiedFilePath)
    {
        if (string.IsNullOrWhiteSpace(copiedFilePath) || !File.Exists(copiedFilePath))
            return;
        try
        {
            File.Delete(copiedFilePath);
        }
        catch
        {
            // Best-effort cleanup; the transaction has already been rolled back.
        }
    }

    public async Task<List<(CertificateCreditTransaction Transaction, Document? Receipt)>> GetTransactionsWithReceiptsAsync(Guid poolId)
    {
        var transactions = await _context.CertificateCreditTransactions
            .AsNoTracking()
            .Where(t => t.PoolId == poolId)
            .OrderByDescending(t => t.TransactionDateTime)
            .ToListAsync();

        var transactionIds = transactions.Select(t => t.Id).ToList();
        var poolReceiptLinks = await _context.DocumentLinks
            .AsNoTracking()
            .Include(l => l.Document)
            .Where(l => l.EntityType == nameof(CertificateCreditTransaction) && transactionIds.Contains(l.EntityId) && l.LinkPurpose == "Receipt")
            .ToListAsync();

        var receiptsByTransaction = poolReceiptLinks.ToDictionary(l => l.EntityId, l => l.Document);

        return transactions.Select(t => (t, receiptsByTransaction.TryGetValue(t.Id, out var doc) ? doc : null)).ToList();
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
            .Where(t => t.PoolId == poolId && (t.TransactionType == CreditTransactionType.Allocate || t.TransactionType == CreditTransactionType.Reserve || t.TransactionType == CreditTransactionType.Release || t.TransactionType == CreditTransactionType.ReallocateOut))
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

    public string GetDocumentFullPath(Document document) => _documentService.GetFullPath(document);
}
