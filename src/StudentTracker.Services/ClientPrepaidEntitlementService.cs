using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class ClientPrepaidEntitlementService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public ClientPrepaidEntitlementService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<List<ClientPrepaidPool>> GetPoolsAsync(bool includeInactive = false) =>
        await _context.ClientPrepaidPools
            .Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<ClientPrepaidPool?> GetPoolAsync(Guid id) =>
        await _context.ClientPrepaidPools.FindAsync(id);

    public async Task<ClientPrepaidPool> CreatePoolAsync(ClientPrepaidPool pool)
    {
        pool.DisplayId = _idGenerator.NextDisplayId<ClientPrepaidPool>("CPP");
        _context.ClientPrepaidPools.Add(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Created", nameof(ClientPrepaidPool), pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task<ClientPrepaidPool> UpdatePoolAsync(ClientPrepaidPool pool)
    {
        pool.UpdatedAt = DateTime.UtcNow;
        _context.ClientPrepaidPools.Update(pool);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", nameof(ClientPrepaidPool), pool.Id, pool.DisplayId);
        await _context.SaveChangesAsync();
        return pool;
    }

    public async Task<ClientPrepaidEntitlementTransaction> AddPrepaidPlacesAsync(
        Guid poolId,
        decimal quantity,
        decimal? monetaryReferenceValue = null,
        string? reason = null,
        Guid? invoiceId = null,
        string? notes = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PrepaidPlacesAdded,
            quantity,
            monetaryReferenceValue,
            reason ?? "Prepaid places added",
            invoiceId,
            notes);

        _audit.Record("PrepaidPlacesAdded", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, Quantity = quantity });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<ClientPrepaidEntitlementTransaction> AddPrepaidValueAsync(
        Guid poolId,
        decimal monetaryValue,
        string? reason = null,
        Guid? invoiceId = null,
        string? notes = null)
    {
        if (monetaryValue <= 0) throw new ArgumentException("Monetary value must be greater than zero.", nameof(monetaryValue));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PrepaidValueAdded,
            0m,
            monetaryValue,
            reason ?? "Prepaid value added",
            invoiceId,
            notes);

        _audit.Record("PrepaidValueAdded", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, MonetaryValue = monetaryValue });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<ClientPrepaidEntitlementTransaction> ReservePlaceAsync(
        Guid poolId,
        Guid allocationId,
        decimal quantity,
        string? reason = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");
        await ValidatePoolRestrictionAsync(pool, allocationId);

        var position = await GetPoolPositionAsync(poolId);
        if (position.UnassignedCarryForward < quantity)
            throw new InvalidOperationException($"Insufficient unassigned places. Available: {position.UnassignedCarryForward}, requested: {quantity} (loaded: {position.PrepaidPlacesLoaded}, reserved: {position.ReservedToNamedStudents}, assigned: {position.ReservedPlaceholders}, unconsumed: {position.TotalUnconsumed})");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PlaceReserved,
            quantity,
            null,
            reason ?? "Place reserved",
            null,
            null,
            allocationId);

        _audit.Record("PlaceReserved", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, AllocationId = allocationId, Quantity = quantity });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<ClientPrepaidEntitlementTransaction> AssignPlaceAsync(
        Guid poolId,
        Guid allocationId,
        decimal quantity,
        string? reason = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");

        var reserved = await GetReservedForAllocationAsync(poolId, allocationId);
        if (reserved < quantity)
            throw new InvalidOperationException($"Insufficient reserved places for this allocation. Reserved: {reserved}, requested: {quantity}");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PlaceAssigned,
            quantity,
            null,
            reason ?? "Place assigned to named student",
            null,
            null,
            allocationId);

        _audit.Record("PlaceAssigned", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, AllocationId = allocationId, Quantity = quantity });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<ClientPrepaidEntitlementTransaction> ReleasePlaceAsync(
        Guid poolId,
        Guid allocationId,
        decimal quantity,
        string? reason = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");

        var allocated = await GetAllocatedAndReservedForAllocationAsync(poolId, allocationId);
        if (allocated < quantity)
            throw new InvalidOperationException($"Insufficient allocated/reserved places for this allocation. Available: {allocated}, requested: {quantity}");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PlaceReleased,
            quantity,
            null,
            reason ?? "Place released",
            null,
            null,
            allocationId);

        _audit.Record("PlaceReleased", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, AllocationId = allocationId, Quantity = quantity });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<ClientPrepaidEntitlementTransaction> ConsumePlaceAsync(
        Guid poolId,
        Guid allocationId,
        decimal quantity,
        string? reason = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");
        await ValidatePoolRestrictionAsync(pool, allocationId);

        var assigned = await GetAssignedForAllocationAsync(poolId, allocationId);
        if (assigned < quantity)
            throw new InvalidOperationException($"Insufficient assigned places for this allocation. Assigned: {assigned}, requested: {quantity}");

        var tx = await CreateTransactionAsync(
            poolId,
            ClientPrepaidEntitlementTransactionType.PlaceConsumed,
            -quantity,
            null,
            reason ?? "Place consumed",
            null,
            null,
            allocationId);

        _audit.Record("PlaceConsumed", nameof(ClientPrepaidEntitlementTransaction), tx.Id, tx.DisplayId, null, new { PoolId = pool.Id, AllocationId = allocationId, Quantity = quantity });
        await _context.SaveChangesAsync();
        return tx;
    }

    public async Task<(ClientPrepaidEntitlementTransaction Out, ClientPrepaidEntitlementTransaction In)> TransferPlaceAsync(
        Guid sourcePoolId,
        Guid targetPoolId,
        decimal quantity,
        string? reason = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (sourcePoolId == targetPoolId) throw new ArgumentException("Source and target pools must differ.");

        var source = await GetPoolPositionAsync(sourcePoolId);
        if (source.UnassignedCarryForward < quantity)
            throw new InvalidOperationException($"Insufficient unassigned places in source pool. Available: {source.UnassignedCarryForward}, requested: {quantity}");

        var outTx = await CreateTransactionAsync(
            sourcePoolId,
            ClientPrepaidEntitlementTransactionType.PlaceTransferred,
            -quantity,
            null,
            reason ?? "Transferred to another pool",
            null,
            null);

        var inTx = await CreateTransactionAsync(
            targetPoolId,
            ClientPrepaidEntitlementTransactionType.PlaceTransferred,
            quantity,
            null,
            reason ?? "Transferred from another pool",
            null,
            null);

        outTx.LinkedTransactionId = inTx.Id;
        inTx.LinkedTransactionId = outTx.Id;

        await _context.SaveChangesAsync();
        _audit.Record("PlaceTransferred", nameof(ClientPrepaidEntitlementTransaction), outTx.Id, outTx.DisplayId, null, new { SourcePoolId = sourcePoolId, TargetPoolId = targetPoolId, Quantity = quantity });
        await _context.SaveChangesAsync();
        return (outTx, inTx);
    }

    public async Task<ClientPrepaidPoolPosition> GetPoolPositionAsync(Guid poolId)
    {
        var pool = await _context.ClientPrepaidPools.FindAsync(poolId) ?? throw new ArgumentException("Pool not found");
        var transactions = await _context.ClientPrepaidEntitlementTransactions
            .Where(t => t.PoolId == poolId)
            .ToListAsync();

        // Total unconsumed loaded places = net of types that affect the pool total.
        var totalTypes = new[]
        {
            ClientPrepaidEntitlementTransactionType.PrepaidPlacesAdded,
            ClientPrepaidEntitlementTransactionType.PrepaidValueAdded,
            ClientPrepaidEntitlementTransactionType.PlaceTransferred,
            ClientPrepaidEntitlementTransactionType.PlaceAdjustment,
            ClientPrepaidEntitlementTransactionType.PlaceReversal,
            ClientPrepaidEntitlementTransactionType.PlaceConsumed
        };
        var loaded = transactions
            .Where(t => totalTypes.Contains(t.TransactionType))
            .Sum(t => t.Quantity);

        var consumed = -transactions
            .Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceConsumed)
            .Sum(t => t.Quantity);

        var allocationGroups = transactions
            .Where(t => t.AllocationId.HasValue
                && (t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReserved
                    || t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceAssigned
                    || t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReleased
                    || t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceConsumed))
            .GroupBy(t => t.AllocationId!.Value)
            .ToList();

        decimal reserved = 0;
        decimal assigned = 0;
        foreach (var group in allocationGroups)
        {
            var totalReserved = group.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReserved).Sum(t => t.Quantity);
            var totalAssigned = group.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceAssigned).Sum(t => t.Quantity);
            var totalReleased = group.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReleased).Sum(t => t.Quantity);

            var remainingReserved = totalReserved - totalAssigned;
            var releaseFromReserved = Math.Min(totalReleased, Math.Max(0, remainingReserved));
            var releaseFromAssigned = totalReleased - releaseFromReserved;

            var netReserved = Math.Max(0, remainingReserved - releaseFromReserved);
            var netAssigned = Math.Max(0, totalAssigned - releaseFromAssigned
                + group.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceConsumed).Sum(t => t.Quantity));

            reserved += netReserved;
            assigned += netAssigned;
        }

        var unassigned = Math.Max(0, loaded - reserved - assigned);

        return new ClientPrepaidPoolPosition
        {
            PoolId = poolId,
            PoolName = pool.Name,
            PrepaidPlacesLoaded = Math.Max(0, loaded + consumed),
            PlacesConsumed = consumed,
            TotalUnconsumed = Math.Max(0, loaded),
            ReservedToNamedStudents = reserved,
            ReservedPlaceholders = 0, // placeholders are tracked via reserved/assigned with no StudentId on the Allocation; service does not inspect Allocation here
            UnassignedCarryForward = unassigned,
            RestrictedToCourseDefinitionId = pool.RestrictedToCourseDefinitionId,
            RestrictedToCourseCategory = pool.RestrictedToCourseCategory
        };
    }

    public async Task<FundingCalculation> CalculateFundingAsync(Guid poolId, decimal requestedPlaces, decimal newPlacesToAdd)
    {
        var position = await GetPoolPositionAsync(poolId);
        var coveredByCarryForward = Math.Min(requestedPlaces, position.UnassignedCarryForward);
        var additionalFundingRequired = Math.Max(0, requestedPlaces - position.UnassignedCarryForward);
        var forecastCarryForward = Math.Max(0, position.UnassignedCarryForward + newPlacesToAdd - requestedPlaces);

        return new FundingCalculation
        {
            RequestedPlaces = requestedPlaces,
            NewPlacesToAdd = newPlacesToAdd,
            CoveredByCarryForward = coveredByCarryForward,
            AdditionalFundingRequired = additionalFundingRequired,
            ForecastCarryForward = forecastCarryForward
        };
    }

    private async Task<ClientPrepaidEntitlementTransaction> CreateTransactionAsync(
        Guid poolId,
        ClientPrepaidEntitlementTransactionType type,
        decimal quantity,
        decimal? monetaryReferenceValue,
        string reason,
        Guid? invoiceId,
        string? notes,
        Guid? allocationId = null)
    {
        var tx = new ClientPrepaidEntitlementTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<ClientPrepaidEntitlementTransaction>("CPT"),
            PoolId = poolId,
            AllocationId = allocationId,
            TransactionType = type,
            Quantity = quantity,
            MonetaryReferenceValue = monetaryReferenceValue,
            InvoiceId = invoiceId,
            Reason = reason,
            Notes = notes,
            TransactionDate = DateTime.UtcNow
        };
        _context.ClientPrepaidEntitlementTransactions.Add(tx);
        await _context.SaveChangesAsync();
        return tx;
    }

    private async Task ValidatePoolRestrictionAsync(ClientPrepaidPool pool, Guid allocationId)
    {
        if (!pool.RestrictedToCourseDefinitionId.HasValue && string.IsNullOrWhiteSpace(pool.RestrictedToCourseCategory))
            return;

        var allocation = await _context.Allocations
            .Include(a => a.CourseDelivery)
            .ThenInclude(d => d!.CourseDefinition)
            .FirstOrDefaultAsync(a => a.Id == allocationId) ?? throw new ArgumentException("Allocation not found");

        var course = allocation.CourseDelivery?.CourseDefinition;
        if (course == null) return;

        if (pool.RestrictedToCourseDefinitionId.HasValue && course.Id != pool.RestrictedToCourseDefinitionId.Value)
            throw new InvalidOperationException("This pool is restricted to a different course.");

        if (!string.IsNullOrWhiteSpace(pool.RestrictedToCourseCategory)
            && !string.Equals(course.Category, pool.RestrictedToCourseCategory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This pool is restricted to a different course category.");
    }

    private async Task<decimal> GetReservedForAllocationAsync(Guid poolId, Guid allocationId)
    {
        var transactions = await _context.ClientPrepaidEntitlementTransactions
            .Where(t => t.PoolId == poolId && t.AllocationId == allocationId)
            .ToListAsync();

        var reserved = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReserved).Sum(t => t.Quantity);
        var assigned = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceAssigned).Sum(t => t.Quantity);
        var released = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReleased).Sum(t => t.Quantity);
        var consumed = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceConsumed).Sum(t => -t.Quantity);

        return Math.Max(0, reserved - assigned - released - consumed);
    }

    private async Task<decimal> GetAssignedForAllocationAsync(Guid poolId, Guid allocationId)
    {
        var transactions = await _context.ClientPrepaidEntitlementTransactions
            .Where(t => t.PoolId == poolId && t.AllocationId == allocationId)
            .ToListAsync();

        var assigned = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceAssigned).Sum(t => t.Quantity);
        var released = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReleased).Sum(t => t.Quantity);
        var reserved = transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceReserved).Sum(t => t.Quantity);

        // releases are applied to reserved first, then assigned
        var remainingReserved = Math.Max(0, reserved - assigned);
        var releaseFromReserved = Math.Min(released, remainingReserved);
        var releaseFromAssigned = released - releaseFromReserved;

        return Math.Max(0, assigned - releaseFromAssigned
            + transactions.Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PlaceConsumed).Sum(t => t.Quantity));
    }

    private async Task<decimal> GetAllocatedAndReservedForAllocationAsync(Guid poolId, Guid allocationId)
    {
        var reserved = await GetReservedForAllocationAsync(poolId, allocationId);
        var assigned = await GetAssignedForAllocationAsync(poolId, allocationId);
        return reserved + assigned;
    }
}

public class ClientPrepaidPoolPosition
{
    public Guid PoolId { get; init; }
    public string PoolName { get; init; } = string.Empty;
    public decimal PrepaidPlacesLoaded { get; init; }
    public decimal PlacesConsumed { get; init; }
    public decimal TotalUnconsumed { get; init; }
    public decimal ReservedToNamedStudents { get; init; }
    public decimal ReservedPlaceholders { get; init; }
    public decimal UnassignedCarryForward { get; init; }
    public Guid? RestrictedToCourseDefinitionId { get; init; }
    public string? RestrictedToCourseCategory { get; init; }
}

public class FundingCalculation
{
    public decimal RequestedPlaces { get; init; }
    public decimal NewPlacesToAdd { get; init; }
    public decimal CoveredByCarryForward { get; init; }
    public decimal AdditionalFundingRequired { get; init; }
    public decimal ForecastCarryForward { get; init; }
}
