using StudentTracker.Core.Common;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class DisplayIdGenerator
{
    private readonly StudentTrackerDbContext _context;

    public DisplayIdGenerator(StudentTrackerDbContext context)
    {
        _context = context;
    }

    public string NextStudentId()
    {
        var settings = _context.AppSettings.First();
        var next = settings.StudentIdSeed++;
        _context.SaveChanges();
        return $"STU-{next:D4}";
    }

    public string NextDeliveryId()
    {
        var settings = _context.AppSettings.First();
        var next = settings.DeliveryIdSeed++;
        _context.SaveChanges();
        return $"DEL-{next:D4}";
    }

    public string NextDisplayId<T>(string prefix) where T : class, IDisplayId
    {
        var existing = _context.Set<T>().AsNoTracking().Where(x => x.DisplayId != null).Select(x => x.DisplayId!).ToList();
        var local = _context.ChangeTracker.Entries<T>()
            .Select(e => e.Entity.DisplayId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        existing.AddRange(local);

        int max = 0;
        foreach (var id in existing)
        {
            if (id.StartsWith(prefix) && int.TryParse(id[prefix.Length..], out var n))
                max = Math.Max(max, n);
        }
        return $"{prefix}-{max + 1:D4}";
    }
}
