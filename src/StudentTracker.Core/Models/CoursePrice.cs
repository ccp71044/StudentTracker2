using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

/// <summary>
/// A completion price for a course, effective from a date. Prices are kept as history so
/// past allocations continue to reflect what they actually cost.
/// </summary>
public class CoursePrice : EntityBase
{
    public Guid CourseDefinitionId { get; set; }
    public CourseDefinition? CourseDefinition { get; set; }
    public decimal CompletionPrice { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public PriceSourceType SourceType { get; set; } = PriceSourceType.Manual;
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
}

public enum PriceSourceType
{
    Manual,
    ProviderPriceList,
    ProviderHistory,
    LegacyRegister
}
