using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class CourseService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public CourseService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<List<CourseDefinition>> GetDefinitionsAsync(string? query = null, bool includeInactive = false)
    {
        var q = _context.CourseDefinitions.Where(c => includeInactive || c.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLower();
            q = q.Where(c => c.CourseCode.ToLower().Contains(lower) || c.CourseTitle.ToLower().Contains(lower));
        }
        return await q.OrderBy(c => c.CourseCode).ToListAsync();
    }

    public async Task<CourseDefinition?> GetDefinitionAsync(Guid id) => await _context.CourseDefinitions.FindAsync(id);

    public async Task<CourseDefinition> CreateDefinitionAsync(CourseDefinition definition)
    {
        _context.CourseDefinitions.Add(definition);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "CourseDefinition", definition.Id, definition.CourseCode);
        await _context.SaveChangesAsync();
        return definition;
    }

    public async Task<CourseDefinition> UpdateDefinitionAsync(CourseDefinition definition)
    {
        definition.UpdatedAt = DateTime.UtcNow;
        _context.CourseDefinitions.Update(definition);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "CourseDefinition", definition.Id, definition.CourseCode);
        await _context.SaveChangesAsync();
        return definition;
    }

    public async Task SetDefinitionActiveAsync(Guid id, bool active)
    {
        var definition = await _context.CourseDefinitions.FindAsync(id) ?? throw new ArgumentException("Course definition not found");
        if (!active)
        {
            var activeDeliveries = await _context.CourseDeliveries.CountAsync(d => d.CourseDefinitionId == id && d.DeliveryStatus != "Cancelled" && d.DeliveryStatus != "Completed");
            if (activeDeliveries > 0)
            {
                _audit.Record("ArchiveBlocked", "CourseDefinition", definition.Id, definition.CourseCode, null, new { ActiveDeliveries = activeDeliveries });
                await _context.SaveChangesAsync();
                throw new InvalidOperationException($"Course has {activeDeliveries} active delivery/deliveries. Complete or cancel them before archiving.");
            }
        }
        definition.IsActive = active;
        definition.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record(active ? "Restored" : "Archived", "CourseDefinition", definition.Id, definition.CourseCode);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CourseDelivery>> GetDeliveriesAsync(string? query = null)
    {
        var q = _context.CourseDeliveries
            .Include(d => d.CourseDefinition)
            .AsNoTracking()
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLower();
            q = q.Where(d =>
                (d.DisplayId != null && d.DisplayId.ToLower().Contains(lower)) ||
                (d.CourseDefinition != null && (d.CourseDefinition.CourseCode.ToLower().Contains(lower) || d.CourseDefinition.CourseTitle.ToLower().Contains(lower))) ||
                (d.Location != null && d.Location.ToLower().Contains(lower)));
        }
        return await q.OrderByDescending(d => d.StartDate).ToListAsync();
    }

    public async Task<CourseDelivery?> GetDeliveryAsync(Guid id) => await _context.CourseDeliveries
        .Include(d => d.CourseDefinition)
        .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<CourseDelivery> CreateDeliveryAsync(CourseDelivery delivery)
    {
        delivery.DisplayId = _idGenerator.NextDisplayId<CourseDelivery>("DEL");
        _context.CourseDeliveries.Add(delivery);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "CourseDelivery", delivery.Id, delivery.DisplayId);
        await _context.SaveChangesAsync();
        return delivery;
    }

    public async Task<CourseDelivery> UpdateDeliveryAsync(CourseDelivery delivery)
    {
        delivery.UpdatedAt = DateTime.UtcNow;
        _context.CourseDeliveries.Update(delivery);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "CourseDelivery", delivery.Id, delivery.DisplayId);
        await _context.SaveChangesAsync();
        return delivery;
    }

    public async Task CancelDeliveryAsync(Guid id)
    {
        var delivery = await _context.CourseDeliveries.FindAsync(id) ?? throw new ArgumentException("Delivery not found");
        var activeAllocations = await _context.Allocations.CountAsync(a => a.CourseDeliveryId == id &&
            a.AllocationStatus != AllocationStatus.Cancelled && a.AllocationStatus != AllocationStatus.Finalised &&
            a.AllocationStatus != AllocationStatus.Withdrawn && a.AllocationStatus != AllocationStatus.Transferred);
        if (activeAllocations > 0)
        {
            _audit.Record("CancellationBlocked", "CourseDelivery", delivery.Id, delivery.DisplayId, null, new { ActiveAllocations = activeAllocations });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException($"Delivery has {activeAllocations} active allocation(s). Resolve them before cancelling the delivery.");
        }
        delivery.DeliveryStatus = "Cancelled";
        delivery.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Cancelled", "CourseDelivery", delivery.Id, delivery.DisplayId);
        await _context.SaveChangesAsync();
    }
}
