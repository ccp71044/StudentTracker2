using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class StudentEditViewModel : ViewModelBase, ICloseable
{
    private readonly StudentService _studentService;
    private readonly Student _student;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Student";

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string? _middleName;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string? _preferredName;

    [ObservableProperty]
    private DateTime? _dateOfBirth;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _employer;

    [ObservableProperty]
    private string? _workGroup;

    [ObservableProperty]
    private string? _employeeNumber;

    [ObservableProperty]
    private string? _usi;

    [ObservableProperty]
    private string _status = "Enrolled";

    [ObservableProperty]
    private string? _manager;

    [ObservableProperty]
    private string? _emergencyContact;

    [ObservableProperty]
    private string? _emergencyPhone;

    [ObservableProperty]
    private string? _groupTag;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isArchived;

    public IReadOnlyList<string> StatusOptions { get; } =
        new[] { "Enrolled", "Completed", "Withdrawn", "Pending", "Rescheduled" };

    public StudentEditViewModel(Student student, StudentService studentService, bool isNew = false)
    {
        _student = student;
        _studentService = studentService;
        _isNew = isNew;
        Title = isNew ? "Add Student" : "Edit Student";
        FirstName = student.FirstName;
        MiddleName = student.MiddleName;
        LastName = student.LastName;
        PreferredName = student.PreferredName;
        DateOfBirth = student.DateOfBirth;
        Email = student.Email;
        Phone = student.Phone;
        Employer = student.Employer;
        WorkGroup = student.WorkGroup;
        EmployeeNumber = student.EmployeeNumber;
        Usi = student.USI;
        Status = student.Status;
        Manager = student.Manager;
        EmergencyContact = student.EmergencyContact;
        EmergencyPhone = student.EmergencyPhone;
        GroupTag = student.GroupTag;
        Notes = student.Notes;
        IsActive = student.IsActive;
        IsArchived = student.IsArchived;
    }

    [RelayCommand]
    private async Task Save()
    {
        _student.FirstName = FirstName;
        _student.MiddleName = MiddleName;
        _student.LastName = LastName;
        _student.PreferredName = PreferredName;
        _student.DateOfBirth = DateOfBirth;
        _student.Email = Email;
        _student.Phone = Phone;
        _student.Employer = Employer;
        _student.WorkGroup = WorkGroup;
        _student.EmployeeNumber = EmployeeNumber;
        _student.USI = Usi;
        _student.Status = Status;
        _student.Manager = Manager;
        _student.EmergencyContact = EmergencyContact;
        _student.EmergencyPhone = EmergencyPhone;
        _student.GroupTag = GroupTag;
        _student.Notes = Notes;
        _student.IsActive = IsActive;
        _student.IsArchived = IsArchived;

        if (_isNew)
        {
            await _studentService.CreateAsync(_student);
        }
        else
        {
            await _studentService.UpdateAsync(_student);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
