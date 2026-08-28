using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class PricingService
{
    private readonly StudentTrackerDbContext _context;

    public PricingService(StudentTrackerDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The price in force for a course on a given date, falling back to the course's default cost.
    /// </summary>
    public async Task<decimal?> GetPriceAsync(Guid courseDefinitionId, DateTime? asAt = null)
    {
        var date = asAt ?? DateTime.UtcNow;
        var price = await _context.CoursePrices
            .Where(p => p.CourseDefinitionId == courseDefinitionId && p.EffectiveFrom <= date)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (price != null)
            return price.CompletionPrice;

        return (await _context.CourseDefinitions.FindAsync(courseDefinitionId))?.DefaultCertificateCost;
    }

    public async Task<List<CoursePrice>> GetPriceHistoryAsync(Guid courseDefinitionId) =>
        await _context.CoursePrices
            .Where(p => p.CourseDefinitionId == courseDefinitionId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();

    /// <summary>
    /// Current price for every active course that has one, keyed by course.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetCurrentPricesAsync(DateTime? asAt = null)
    {
        var date = asAt ?? DateTime.UtcNow;
        var prices = await _context.CoursePrices
            .Where(p => p.EffectiveFrom <= date)
            .ToListAsync();

        var current = prices
            .GroupBy(p => p.CourseDefinitionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveFrom).First().CompletionPrice);

        var courses = await _context.CourseDefinitions.Where(c => c.IsActive).ToListAsync();
        foreach (var course in courses)
        {
            if (!current.ContainsKey(course.Id) && course.DefaultCertificateCost.HasValue)
                current[course.Id] = course.DefaultCertificateCost.Value;
        }

        return current;
    }

    public async Task<CoursePrice> SetPriceAsync(Guid courseDefinitionId, decimal price, DateTime effectiveFrom, PriceSourceType sourceType = PriceSourceType.Manual, string? sourceReference = null)
    {
        var entry = new CoursePrice
        {
            CourseDefinitionId = courseDefinitionId,
            CompletionPrice = price,
            EffectiveFrom = effectiveFrom,
            SourceType = sourceType,
            SourceReference = sourceReference
        };
        _context.CoursePrices.Add(entry);

        var course = await _context.CourseDefinitions.FindAsync(courseDefinitionId);
        if (course != null)
        {
            course.DefaultCertificateCost = price;
            course.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return entry;
    }
}
