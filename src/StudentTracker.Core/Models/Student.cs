using StudentTracker.Core.Common;

namespace StudentTracker.Core.Models;

public class Student : EntityBase, IDisplayId
{
    public string? DisplayId { get; set; }
    /// <summary>The provider's own student number, used to match a student across provider exports.</summary>
    public string? ProviderStudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Employer { get; set; }
    public string? WorkGroup { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? USI { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Enrolled";
    public string? Manager { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? GroupTag { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public bool PotentialDuplicate { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
