using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Wpf.ViewModels;

public partial class CourseDeliveryOverviewViewModel : ViewModelBase
{
    private readonly StudentTrackerDbContext _context;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _deliveries = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CourseTitle), nameof(DeliveryDisplayId), nameof(Spaces), nameof(TotalCapacity), nameof(EnrolledCount))]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private ObservableCollection<DeliveryStudentItem> _students = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCourseNotesCommand))]
    private string? _courseNotes;

    public string CourseTitle => SelectedDelivery?.CourseDefinition?.CourseTitle ?? string.Empty;
    public string DeliveryDisplayId => SelectedDelivery?.DisplayId ?? string.Empty;
    public int? TotalCapacity => SelectedDelivery?.Capacity;
    public int EnrolledCount => Students.Count;
    public int? Spaces => TotalCapacity.HasValue ? TotalCapacity.Value - EnrolledCount : null;

    public CourseDeliveryOverviewViewModel(StudentTrackerDbContext context)
    {
        _context = context;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus != "Cancelled")
            .Include(d => d.CourseDefinition)
            .OrderBy(d => d.StartDate)
            .AsNoTracking()
            .ToListAsync();

        foreach (var d in list)
            Deliveries.Add(d);

        if (Deliveries.Any())
            SelectedDelivery = Deliveries.First();
    }

    partial void OnSelectedDeliveryChanged(CourseDelivery? value)
    {
        if (value == null)
        {
            Students.Clear();
            CourseNotes = null;
            return;
        }

        CourseNotes = value.Notes;
        _ = LoadStudentsAsync(value.Id);
    }

    private async Task LoadStudentsAsync(Guid deliveryId)
    {
        var allocations = await _context.Allocations
            .Where(a => a.CourseDeliveryId == deliveryId && a.AllocationStatus != Core.Enums.AllocationStatus.Cancelled)
            .Include(a => a.Student)
            .Include(a => a.ClientPrepaidPool)
            .Include(a => a.BudgetPool)
            .Include(a => a.CreditPool)
            .AsNoTracking()
            .ToListAsync();

        Students.Clear();

        foreach (var a in allocations)
        {
            Students.Add(new DeliveryStudentItem
            {
                AllocationDisplayId = a.DisplayId,
                StudentName = a.Student?.FullName ?? "—",
                StudentEmail = a.Student?.Email,
                Outcome = a.OutcomeStatus.ToString(),
                FundingSource = a.ClientPrepaidPool?.Name ?? a.BudgetPool?.Name ?? a.CreditPool?.Name ?? "—",
                Notes = a.Notes
            });
        }
    }

    [RelayCommand]
    private async Task SaveCourseNotes()
    {
        if (SelectedDelivery == null) return;

        var delivery = await _context.CourseDeliveries.FindAsync(SelectedDelivery.Id);
        if (delivery == null) return;

        delivery.Notes = CourseNotes;
        await _context.SaveChangesAsync();
    }
}

public class DeliveryStudentItem
{
    public string? AllocationDisplayId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
