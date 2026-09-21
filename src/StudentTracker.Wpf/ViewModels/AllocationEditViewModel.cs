using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class AllocationEditViewModel : ViewModelBase, ICloseable
{
    private readonly AllocationService _allocationService;
    private readonly StudentService _studentService;
    private readonly CourseService _courseService;
    private readonly CreditService _creditService;
    private readonly BudgetService _budgetService;
    private readonly StudentTrackerDbContext _context;
    private readonly Allocation _allocation;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Allocation";

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private ObservableCollection<Student> _availableStudents = new();

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _availableDeliveries = new();

    [ObservableProperty]
    private ObservableCollection<CertificateCreditPool> _availableCreditPools = new();

    [ObservableProperty]
    private ObservableCollection<BudgetPool> _availableBudgetPools = new();

    [ObservableProperty]
    private CertificateCreditPool? _selectedCreditPool;

    [ObservableProperty]
    private BudgetPool? _selectedBudgetPool;

    [ObservableProperty]
    private decimal? _certificateCost;

    [ObservableProperty]
    private AllocationStatus _allocationStatus = AllocationStatus.Enrolled;

    [ObservableProperty]
    private AttendanceStatus _attendanceStatus = AttendanceStatus.NotRecorded;

    [ObservableProperty]
    private OutcomeStatus _outcomeStatus = OutcomeStatus.Pending;

    [ObservableProperty]
    private DateTime? _outcomeDate;

    [ObservableProperty]
    private string? _outcomeNotes;

    [ObservableProperty]
    private string? _notes;

    public IReadOnlyList<AllocationStatus> AllocationStatusOptions { get; } = Enum.GetValues<AllocationStatus>();
    public IReadOnlyList<AttendanceStatus> AttendanceStatusOptions { get; } = Enum.GetValues<AttendanceStatus>();
    public IReadOnlyList<OutcomeStatus> OutcomeStatusOptions { get; } = Enum.GetValues<OutcomeStatus>();

    public AllocationEditViewModel(Allocation allocation, AllocationService allocationService, StudentService studentService, CourseService courseService, CreditService creditService, BudgetService budgetService, StudentTrackerDbContext context, bool isNew = false)
    {
        _allocation = allocation;
        _allocationService = allocationService;
        _studentService = studentService;
        _courseService = courseService;
        _creditService = creditService;
        _budgetService = budgetService;
        _context = context;
        _isNew = isNew;
        Title = isNew ? "Add Allocation" : "Edit Allocation";

        // Initialize with empty collections - load data when dialog opens
        AvailableStudents = new ObservableCollection<Student>();
        AvailableDeliveries = new ObservableCollection<CourseDelivery>();
        AvailableCreditPools = new ObservableCollection<CertificateCreditPool>();
        AvailableBudgetPools = new ObservableCollection<BudgetPool>();

        if (!_isNew)
        {
            SelectedStudent = _allocation.Student;
            SelectedDelivery = _allocation.CourseDelivery;
            SelectedCreditPool = _allocation.CreditPool;
            SelectedBudgetPool = _allocation.BudgetPool;
            CertificateCost = _allocation.CertificateCost;
            AllocationStatus = _allocation.AllocationStatus;
            AttendanceStatus = _allocation.AttendanceStatus;
            OutcomeStatus = _allocation.OutcomeStatus;
            OutcomeDate = _allocation.OutcomeDate;
            OutcomeNotes = _allocation.OutcomeNotes;
            Notes = _allocation.Notes;
        }
    }

    public async Task LoadDataAsync()
    {
        var students = await _studentService.SearchAsync(string.Empty);
        var deliveries = await _courseService.GetDeliveriesAsync();
        var creditPools = await _creditService.GetPoolsAsync();
        var budgetPools = await _budgetService.GetPoolsAsync();

        AvailableStudents = new ObservableCollection<Student>(students);
        AvailableDeliveries = new ObservableCollection<CourseDelivery>(deliveries);
        AvailableCreditPools = new ObservableCollection<CertificateCreditPool>(creditPools);
        AvailableBudgetPools = new ObservableCollection<BudgetPool>(budgetPools);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedStudent == null)
        {
            // Show error - student required
            return;
        }

        if (SelectedDelivery == null)
        {
            // Show error - delivery required
            return;
        }

        if (_isNew)
        {
            var result = await _allocationService.AllocateStudentAsync(
                SelectedDelivery.Id,
                SelectedStudent!.Id,
                CertificateCost,
                SelectedBudgetPool?.Id,
                SelectedCreditPool?.Id);
            result.Notes = Notes;
            await _context.SaveChangesAsync();
        }
        else
        {
            // Update existing allocation
            if (SelectedStudent != null && _allocation.StudentId != SelectedStudent.Id)
            {
                _allocation.StudentId = SelectedStudent.Id;
            }

            if (SelectedDelivery != null && _allocation.CourseDeliveryId != SelectedDelivery.Id)
            {
                _allocation.CourseDeliveryId = SelectedDelivery.Id;
            }

            _allocation.CertificateCost = CertificateCost;
            _allocation.BudgetPoolId = SelectedBudgetPool?.Id;
            _allocation.CreditPoolId = SelectedCreditPool?.Id;
            _allocation.AllocationStatus = AllocationStatus;
            _allocation.AttendanceStatus = AttendanceStatus;
            _allocation.OutcomeStatus = OutcomeStatus;
            _allocation.OutcomeDate = OutcomeDate;
            _allocation.OutcomeNotes = OutcomeNotes;
            _allocation.Notes = Notes;
            await _allocationService.MarkAttendanceAsync(_allocation.Id, AttendanceStatus, OutcomeNotes);
            await _allocationService.MarkOutcomeAsync(_allocation.Id, OutcomeStatus, null, OutcomeNotes, OutcomeDate);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}