using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;

namespace StudentTracker.Core.Models;

public class CourseDelivery : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    public Guid CourseDefinitionId { get; set; }
    public CourseDefinition? CourseDefinition { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DeliveryDateStatus DateStatus { get; set; } = DeliveryDateStatus.Confirmed;
    public string? Location { get; set; }
    public string? TrainerName { get; set; }
    public string? TrainerBusinessDetails { get; set; }
    public int? Capacity { get; set; }
    public string? DeliveryStatus { get; set; } = "Scheduled";
    public string? Notes { get; set; }
}
