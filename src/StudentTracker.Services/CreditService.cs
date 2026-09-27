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
        var tx = NewTransaction(poolId, CreditTransactionType.TopUp, amount, quantity, reason ?? "Top-up");
        tx.SourceType = sourceType;
        tx.ExternalPurchaseReference = externalRef;
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

    /// <summary>
    /// The ledger amount one certificate costs a pool: the cash price for a monetary pool, or a
    /// single unit for a pool that is counted in certificates.
    /// </summary>
    public async Task<decimal> GetUnitAmountAsync(Guid poolId, decimal? certificateCost)
    {
        var pool = await _context.CertificateCreditPools.FindAsync(poolId)
            ?? throw new ArgumentException("Credit pool not found");
        return pool.UnitType == CreditUnitType.Monetary ? certificateCost ?? 0m : 1m;
    }

    /// <summary>
    /// The credit still reserved against a single allocation, used when releasing or consuming
    /// so that the amount always matches what was originally reserved.
    /// </summary>
    public async Task<decimal> GetReservedForAllocationAsync(Guid poolId, Guid allocationId)
    {
        var rows = await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId && t.AllocationId == allocationId)
            .Select(t => new { t.TransactionType, t.Amount })
            .ToListAsync();

        decimal reserved = 0m;
        foreach (var row in rows)
        {
            var magnitude = Math.Abs(row.Amount);
            switch (row.TransactionType)
            {
                case CreditTransactionType.Allocate:
                case CreditTransactionType.Reserve:
                case CreditTransactionType.ReallocateIn:
                    reserved += magnitude;
                    break;
                case CreditTransactionType.Release:
                case CreditTransactionType.ReallocateOut:
                case CreditTransactionType.OrderConsume:
                case CreditTransactionType.ManualConsume:
                    reserved -= magnitude;
                    break;
            }
        }

        return Math.Max(0m, reserved);
    }

    /// <summary>
    /// Reserves credit against an allocation. Over-allocation is blocked unless an explicit
    /// override reason is supplied (business rule 8.6.6).
    /// </summary>
    public async Task<CertificateCreditTransaction> AllocateAsync(Guid poolId, Guid allocationId, decimal amount, decimal? quantity = null, string? reason = null, string? overrideReason = null)
    {
        var available = (await GetBalanceAsync(poolId)).Available;
        if (amount > available && string.IsNullOrWhiteSpace(overrideReason))
            throw new InvalidOperationException($"Insufficient credit available. Available: {available}, requested: {amount}");

        var tx = NewTransaction(poolId, CreditTransactionType.Allocate, amount, quantity, reason ?? "Allocated to student");
        tx.AllocationId = allocationId;
        tx.Notes = overrideReason is null ? null : $"Negative balance override: {overrideReason}";
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditPoolId = poolId;
        allocation.CreditStatus = CreditStatus.Allocated;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditAllocated", "Allocation", allocation.Id, allocation.DisplayId, null, new { CreditTransactionId = tx.DisplayId });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> ConsumeAsync(Guid poolId, Guid allocationId, decimal amount, decimal? quantity = null, string? reason = null, CreditTransactionType type = CreditTransactionType.OrderConsume)
    {
        var tx = NewTransaction(poolId, type, amount, quantity, reason ?? "Certificate ordered");
        tx.AllocationId = allocationId;
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
        var tx = NewTransaction(poolId, CreditTransactionType.Release, amount, null, reason ?? "Credit released");
        tx.AllocationId = allocationId;
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditStatus = CreditStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditReleased", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    /// <summary>
    /// Marks reserved credit as lost to the pool, for example a withdrawal with insufficient
    /// notice to reallocate the position (business rule 8.4).
    /// </summary>
    public async Task<CertificateCreditTransaction> MarkUnavailableAsync(Guid poolId, Guid allocationId, decimal amount, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required when credit is made unavailable.", nameof(reason));

        var tx = NewTransaction(poolId, CreditTransactionType.ManualConsume, amount, null, reason);
        tx.AllocationId = allocationId;
        tx.IsCreditLoss = true;
        _context.CertificateCreditTransactions.Add(tx);

        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        allocation.CreditStatus = CreditStatus.Unavailable;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditUnavailable", "Allocation", allocation.Id, allocation.DisplayId, null, new { Reason = reason });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> ExpireAsync(Guid poolId, decimal amount, string? reason = null)
    {
        var tx = NewTransaction(poolId, CreditTransactionType.Expire, amount, null, reason ?? "Credit expired");
        _context.CertificateCreditTransactions.Add(tx);
        await _context.SaveChangesAsync();
        _audit.Record("CreditExpired", "CertificateCreditTransaction", tx.Id, tx.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<CertificateCreditTransaction> AdjustAsync(Guid poolId, decimal signedAmount, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required for a credit adjustment.", nameof(reason));

        var tx = NewTransaction(poolId, CreditTransactionType.Adjustment, signedAmount, null, reason);
        tx.Amount = signedAmount;
        _context.CertificateCreditTransactions.Add(tx);
        await _context.SaveChangesAsync();
        _audit.Record("CreditAdjusted", "CertificateCreditTransaction", tx.Id, tx.DisplayId);
        await _context.SaveChangesAsync();
        return tx;
    }

    /// <summary>
    /// Reverses an existing transaction. The original row is retained as evidence; both it and the
    /// reversal are excluded from balance calculations.
    /// </summary>
    public async Task<CertificateCreditTransaction> ReverseAsync(Guid transactionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to reverse a transaction.", nameof(reason));

        var original = await _context.CertificateCreditTransactions.FindAsync(transactionId)
            ?? throw new ArgumentException("Transaction not found");

        var alreadyReversed = await _context.CertificateCreditTransactions
            .AnyAsync(t => t.TransactionType == CreditTransactionType.Reversal && t.LinkedTransactionId == transactionId);
        if (alreadyReversed)
            throw new InvalidOperationException($"Transaction {original.DisplayId} has already been reversed.");

        var tx = NewTransaction(original.PoolId, CreditTransactionType.Reversal, original.Amount, original.Quantity, reason);
        tx.Amount = -original.Amount;
        tx.AllocationId = original.AllocationId;
        tx.LinkedTransactionId = original.Id;
        _context.CertificateCreditTransactions.Add(tx);
        await _context.SaveChangesAsync();
        _audit.Record("CreditReversed", "CertificateCreditTransaction", original.Id, original.DisplayId, null, new { Reason = reason });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<(CertificateCreditTransaction Out, CertificateCreditTransaction In)> ReallocateAsync(Guid sourcePoolId, Guid targetPoolId, Guid sourceAllocationId, Guid targetAllocationId, decimal amount, string? reason = null)
    {
        var outTx = NewTransaction(sourcePoolId, CreditTransactionType.ReallocateOut, amount, null, reason ?? "Reallocated out");
        outTx.AllocationId = sourceAllocationId;
        _context.CertificateCreditTransactions.Add(outTx);
        await _context.SaveChangesAsync();

        var inTx = NewTransaction(targetPoolId, CreditTransactionType.ReallocateIn, amount, null, reason ?? "Reallocated in");
        inTx.AllocationId = targetAllocationId;
        inTx.LinkedTransactionId = outTx.Id;
        _context.CertificateCreditTransactions.Add(inTx);

        outTx.LinkedTransactionId = inTx.Id;

        var sourceAllocation = await _context.Allocations.FindAsync(sourceAllocationId) ?? throw new ArgumentException("Source allocation not found");
        sourceAllocation.CreditStatus = CreditStatus.Reallocated;
        sourceAllocation.UpdatedAt = DateTime.UtcNow;

        var targetAllocation = await _context.Allocations.FindAsync(targetAllocationId) ?? throw new ArgumentException("Target allocation not found");
        targetAllocation.CreditPoolId = targetPoolId;
        targetAllocation.CreditStatus = CreditStatus.Allocated;
        targetAllocation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _audit.Record("CreditReallocated", "Allocation", sourceAllocation.Id, sourceAllocation.DisplayId, null, new { TargetAllocationId = targetAllocation.DisplayId });
        await _context.SaveChangesAsync();
        return (outTx, inTx);
    }

    /// <summary>
    /// Calculates the pool balance from its transactions (design section 10.1). Reversed
    /// transactions and their reversals are excluded from every figure.
    /// </summary>
    public async Task<CreditPoolBalance> GetBalanceAsync(Guid poolId)
    {
        var rows = await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId)
            .Select(t => new { t.Id, t.TransactionType, t.Amount, t.LinkedTransactionId, t.IsCreditLoss })
            .ToListAsync();

        var reversedIds = rows
            .Where(t => t.TransactionType == CreditTransactionType.Reversal && t.LinkedTransactionId.HasValue)
            .Select(t => t.LinkedTransactionId!.Value)
            .ToHashSet();

        decimal loaded = 0m, adjustments = 0m, reserved = 0m, consumed = 0m, released = 0m, expired = 0m, unavailable = 0m;

        foreach (var row in rows)
        {
            if (row.TransactionType == CreditTransactionType.Reversal || reversedIds.Contains(row.Id))
                continue;

            var magnitude = Math.Abs(row.Amount);
            switch (row.TransactionType)
            {
                case CreditTransactionType.TopUp:
                    loaded += magnitude;
                    break;
                case CreditTransactionType.Adjustment:
                    adjustments += row.Amount;
                    break;
                case CreditTransactionType.Allocate:
                case CreditTransactionType.Reserve:
                case CreditTransactionType.ReallocateIn:
                    reserved += magnitude;
                    break;
                case CreditTransactionType.Release:
                case CreditTransactionType.ReallocateOut:
                    released += magnitude;
                    break;
                case CreditTransactionType.OrderConsume:
                    consumed += magnitude;
                    break;
                case CreditTransactionType.ManualConsume:
                    if (row.IsCreditLoss)
                        unavailable += magnitude;
                    else
                        consumed += magnitude;
                    break;
                case CreditTransactionType.Expire:
                    expired += magnitude;
                    break;
            }
        }

        // Consuming or releasing a reservation retires it, so it no longer counts as allocated.
        // Credit consumed without ever being reserved - imported provider history, for example -
        // must not drive this negative, or it would cancel out the consumption in Available.
        var allocated = Math.Max(0m, reserved - released - consumed - unavailable);

        return new CreditPoolBalance(loaded, adjustments, allocated, consumed, released, expired, unavailable);
    }

    public async Task<decimal> GetLoadedAsync(Guid poolId) => (await GetBalanceAsync(poolId)).Loaded;
    public async Task<decimal> GetAllocatedAsync(Guid poolId) => (await GetBalanceAsync(poolId)).Allocated;
    public async Task<decimal> GetConsumedAsync(Guid poolId) => (await GetBalanceAsync(poolId)).Consumed;
    public async Task<decimal> GetExpiredAsync(Guid poolId) => (await GetBalanceAsync(poolId)).Expired;
    public async Task<decimal> GetAvailableAsync(Guid poolId) => (await GetBalanceAsync(poolId)).Available;

    public async Task<List<CertificateCreditTransaction>> GetTransactionsAsync(Guid poolId) =>
        await _context.CertificateCreditTransactions
            .Where(t => t.PoolId == poolId)
            .OrderByDescending(t => t.TransactionDateTime)
            .Include(t => t.Allocation).ThenInclude(a => a!.Student)
            .ToListAsync();

    /// <summary>
    /// Credit that was consumed or made unavailable without the student completing the course.
    /// </summary>
    public async Task<List<CertificateCreditTransaction>> GetConsumedWithoutCompletionAsync() =>
        await _context.CertificateCreditTransactions
            .Where(t => (t.TransactionType == CreditTransactionType.OrderConsume || t.TransactionType == CreditTransactionType.ManualConsume)
                        && t.Allocation != null
                        && t.Allocation.OutcomeStatus != OutcomeStatus.Completed)
            .Include(t => t.Allocation).ThenInclude(a => a!.Student)
            .Include(t => t.Allocation).ThenInclude(a => a!.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .OrderByDescending(t => t.TransactionDateTime)
            .ToListAsync();

    private CertificateCreditTransaction NewTransaction(Guid poolId, CreditTransactionType type, decimal amount, decimal? quantity, string reason) => new()
    {
        DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
        PoolId = poolId,
        TransactionType = type,
        Amount = Math.Abs(amount),
        Quantity = quantity,
        Reason = reason,
        TransactionDateTime = DateTime.UtcNow
    };

    public string GetDocumentFullPath(Document document) => _documentService.GetFullPath(document);
}
