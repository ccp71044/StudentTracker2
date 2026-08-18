using System.Text.Json;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class AuditService
{
    private readonly StudentTrackerDbContext _context;

    public AuditService(StudentTrackerDbContext context)
    {
        _context = context;
    }

    public void Record(string action, string entityType, Guid entityId, string? displayId = null,
        object? oldValues = null, object? newValues = null, string? reason = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityDisplayId = displayId,
            OldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });
    }
}
