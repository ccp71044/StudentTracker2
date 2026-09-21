using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class AllocationSelection : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public Allocation Allocation { get; }
    public string DisplayName => Allocation.Student?.FullName ?? Allocation.PlaceholderName ?? "Unknown";
    public string AttendanceText => Allocation.AttendanceStatus.ToString();
    public string OutcomeText => Allocation.OutcomeStatus.ToString();

    public AllocationSelection(Allocation allocation, bool isSelected)
    {
        Allocation = allocation;
        _isSelected = isSelected;
    }
}

public partial class SignOffEditViewModel : ViewModelBase, ICloseable
{
    private readonly CourseDelivery _delivery;
    private readonly SignOffService _signOffService;
    private readonly PdfService _pdfService;
    private readonly DocumentService _documentService;
    private readonly IDialogService _dialogService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    private string _statusText = "Draft";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSignOff))]
    private Guid? _signOffId;

    public bool IsLocked => StatusText == SignOffStatus.Signed.ToString();
    public bool IsEditable => !IsLocked;
    public bool HasSignOff => SignOffId.HasValue;

    [ObservableProperty]
    private string _trainerName = string.Empty;

    [ObservableProperty]
    private string? _trainerDetails;

    [ObservableProperty]
    private string? _authorisedByName;

    [ObservableProperty]
    private string? _authorisedByPosition;

    [ObservableProperty]
    private string? _verifiedByName;

    [ObservableProperty]
    private string? _verifiedByPosition;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private DateTime? _trainerSignedDate;

    [ObservableProperty]
    private DateTime? _authorisedSignedDate;

    [ObservableProperty]
    private DateTime? _verifiedSignedDate;

    [ObservableProperty]
    private ObservableCollection<AllocationSelection> _allocations = new();

    [ObservableProperty]
    private string? _linkedDocumentInfo;

    public SignOffEditViewModel(
        SignOff? existing,
        CourseDelivery delivery,
        List<Allocation> deliveryAllocations,
        SignOffService signOffService,
        PdfService pdfService,
        DocumentService documentService,
        IDialogService dialogService)
    {
        _delivery = delivery;
        _signOffService = signOffService;
        _pdfService = pdfService;
        _documentService = documentService;
        _dialogService = dialogService;

        if (existing != null)
        {
            _title = $"Sign-Off {existing.DisplayId} (v{existing.Version})";
            _signOffId = existing.Id;
            _statusText = existing.Status.ToString();
            _trainerName = existing.TrainerName ?? string.Empty;
            _trainerDetails = existing.TrainerDetails;
            _authorisedByName = existing.AuthorisedByName;
            _authorisedByPosition = existing.AuthorisedByPosition;
            _verifiedByName = existing.VerifiedByName;
            _verifiedByPosition = existing.VerifiedByPosition;
            _notes = existing.Notes;
            _trainerSignedDate = existing.TrainerSignedDate;
            _authorisedSignedDate = existing.AuthorisedSignedDate;
            _verifiedSignedDate = existing.VerifiedSignedDate;
            _linkedDocumentInfo = existing.FileDocument?.DisplayName ?? (existing.FileDocumentId.HasValue ? "Linked" : null);

            var participantAllocationIds = existing.Participants
                .Where(p => p.AllocationId.HasValue)
                .Select(p => p.AllocationId!.Value)
                .ToHashSet();
            Allocations = new ObservableCollection<AllocationSelection>(
                deliveryAllocations.Select(a => new AllocationSelection(a, participantAllocationIds.Contains(a.Id))));
        }
        else
        {
            _title = "New Record of Completion";
            _trainerName = delivery.TrainerName ?? string.Empty;
            _trainerDetails = delivery.TrainerBusinessDetails;
            Allocations = new ObservableCollection<AllocationSelection>(
                deliveryAllocations.Select(a => new AllocationSelection(a,
                    a.AttendanceStatus == AttendanceStatus.Attended ||
                    a.AttendanceStatus == AttendanceStatus.Confirmed ||
                    a.OutcomeStatus == OutcomeStatus.Completed)));
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var a in Allocations) a.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var a in Allocations) a.IsSelected = false;
    }

    [RelayCommand]
    private async Task GeneratePdf()
    {
        var selected = Allocations.Where(a => a.IsSelected).Select(a => a.Allocation.Id).ToList();
        if (selected.Count == 0)
        {
            _dialogService.ShowError("Select at least one participant to generate a sign-off.");
            return;
        }

        if (string.IsNullOrWhiteSpace(TrainerName))
        {
            _dialogService.ShowError("Trainer name is required.");
            return;
        }

        try
        {
            // If regenerating, supersede the existing draft (keeps it as history)
            if (SignOffId.HasValue)
            {
                await _signOffService.SupersedeAsync(SignOffId.Value);
            }

            var signOff = await _signOffService.GenerateDraftAsync(_delivery.Id, selected, TrainerName, TrainerDetails);

            // Apply form details that GenerateDraft may not have picked up
            await _signOffService.UpdateDetailsAsync(signOff.Id,
                TrainerName, TrainerDetails,
                AuthorisedByName, AuthorisedByPosition,
                VerifiedByName, VerifiedByPosition, Notes);

            if (TrainerSignedDate.HasValue || AuthorisedSignedDate.HasValue || VerifiedSignedDate.HasValue)
                await _signOffService.UpdateSignedDatesAsync(signOff.Id, TrainerSignedDate, AuthorisedSignedDate, VerifiedSignedDate);

            // Generate PDF bytes, save via DocumentService
            var pdfBytes = _pdfService.GenerateSignOffPdf(signOff.Id);
            var tempPath = Path.Combine(Path.GetTempPath(), $"SignOff_{signOff.DisplayId}_v{signOff.Version}.pdf");
            await File.WriteAllBytesAsync(tempPath, pdfBytes);

            var doc = await _documentService.AddDocumentAsync(
                tempPath, "SignOffs",
                $"Sign-Off {signOff.DisplayId} v{signOff.Version}",
                $"Record of completion for {_delivery.DisplayId}",
                "application/pdf",
                DateTime.Now);

            await _signOffService.SetFileDocumentIdAsync(signOff.Id, doc.Id);

            // Link document to delivery, sign-off, each participant allocation + student
            await LinkDocumentSafely(doc.Id, "CourseDelivery", _delivery.Id, "Record of Completion");
            await LinkDocumentSafely(doc.Id, "SignOff", signOff.Id, "Generated PDF");

            var fullSignOff = await _signOffService.GetAsync(signOff.Id);
            if (fullSignOff != null)
            {
                foreach (var p in fullSignOff.Participants)
                {
                    if (p.AllocationId.HasValue)
                        await LinkDocumentSafely(doc.Id, "Allocation", p.AllocationId.Value, "Record of Completion");
                    var alloc = Allocations.FirstOrDefault(a => a.Allocation.Id == p.AllocationId);
                    if (alloc?.Allocation.StudentId != null)
                        await LinkDocumentSafely(doc.Id, "Student", alloc.Allocation.StudentId.Value, "Record of Completion");
                }
            }

            try { File.Delete(tempPath); } catch { /* best effort cleanup */ }

            // Update local state so HasSignOff/buttons become visible
            SignOffId = signOff.Id;
            StatusText = signOff.Status.ToString();
            LinkedDocumentInfo = doc.DisplayName;
            Title = $"Sign-Off {signOff.DisplayId} (v{signOff.Version})";

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Failed to generate sign-off PDF.", ex);
        }
    }

    [RelayCommand]
    private async Task ImportSignedPdf()
    {
        if (SignOffId == null)
        {
            _dialogService.ShowError("Generate a draft sign-off first before importing a signed copy.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select signed PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var signOff = await _signOffService.GetAsync(SignOffId.Value);
            if (signOff == null) return;

            // Import the signed PDF as a new managed document
            var doc = await _documentService.AddDocumentAsync(
                dialog.FileName, "SignOffs",
                $"Signed — {signOff.DisplayId} v{signOff.Version}",
                $"Signed record of completion for {_delivery.DisplayId}",
                "application/pdf",
                DateTime.Now);

            // Point the sign-off at the signed document.
            // The original generated-draft Document and its DocumentLinks remain intact
            // for full traceability — we do NOT archive/delete the draft document.
            await _signOffService.SetFileDocumentIdAsync(signOff.Id, doc.Id);

            // Promote status to ReadyForSignature to indicate a signed copy is attached
            if (signOff.Status == SignOffStatus.Draft)
            {
                await _signOffService.SetStatusReadyForSignatureAsync(signOff.Id);
                StatusText = SignOffStatus.ReadyForSignature.ToString();
            }

            // Persist any signed-date / detail edits made on the form
            if (IsEditable)
            {
                await _signOffService.UpdateDetailsAsync(signOff.Id,
                    TrainerName, TrainerDetails,
                    AuthorisedByName, AuthorisedByPosition,
                    VerifiedByName, VerifiedByPosition, Notes);
                await _signOffService.UpdateSignedDatesAsync(signOff.Id,
                    TrainerSignedDate, AuthorisedSignedDate, VerifiedSignedDate);
            }

            // Link the signed document to delivery, sign-off, allocations & students
            await LinkDocumentSafely(doc.Id, "CourseDelivery", _delivery.Id, "Signed Record of Completion");
            await LinkDocumentSafely(doc.Id, "SignOff", signOff.Id, "Signed PDF");

            foreach (var p in signOff.Participants)
            {
                if (p.AllocationId.HasValue)
                    await LinkDocumentSafely(doc.Id, "Allocation", p.AllocationId.Value, "Signed Record of Completion");
                var alloc = Allocations.FirstOrDefault(a => a.Allocation.Id == p.AllocationId);
                if (alloc?.Allocation.StudentId != null)
                    await LinkDocumentSafely(doc.Id, "Student", alloc.Allocation.StudentId.Value, "Signed Record of Completion");
            }

            LinkedDocumentInfo = doc.DisplayName;
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Failed to import signed PDF.", ex);
        }
    }

    [RelayCommand]
    private async Task OpenPdf()
    {
        if (SignOffId == null) return;
        try
        {
            var signOff = await _signOffService.GetAsync(SignOffId.Value);
            if (signOff?.FileDocumentId == null)
            {
                _dialogService.ShowError("No PDF is attached to this sign-off.");
                return;
            }
            var path = _documentService.GetDocumentPath(signOff.FileDocumentId.Value);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                _dialogService.ShowError("The PDF file could not be found on disk.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Could not open the PDF.", ex);
        }
    }

    [RelayCommand]
    private async Task LockAsSigned()
    {
        if (SignOffId == null) return;
        if (!_dialogService.Confirm("Lock this sign-off as Signed? This cannot be undone.")) return;

        try
        {
            // Persist any last signed-date edits before locking
            if (TrainerSignedDate.HasValue || AuthorisedSignedDate.HasValue || VerifiedSignedDate.HasValue)
                await _signOffService.UpdateSignedDatesAsync(SignOffId.Value, TrainerSignedDate, AuthorisedSignedDate, VerifiedSignedDate);

            await _signOffService.LockAsync(SignOffId.Value);
            StatusText = SignOffStatus.Signed.ToString();
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Failed to lock sign-off.", ex);
        }
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        if (SignOffId == null) return;
        try
        {
            await _signOffService.UpdateDetailsAsync(SignOffId.Value,
                TrainerName, TrainerDetails,
                AuthorisedByName, AuthorisedByPosition,
                VerifiedByName, VerifiedByPosition, Notes);
            await _signOffService.UpdateSignedDatesAsync(SignOffId.Value, TrainerSignedDate, AuthorisedSignedDate, VerifiedSignedDate);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Failed to save sign-off details.", ex);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private async Task LinkDocumentSafely(Guid documentId, string entityType, Guid entityId, string? purpose)
    {
        try
        {
            await _documentService.LinkDocumentAsync(documentId, entityType, entityId, purpose);
        }
        catch (InvalidOperationException)
        {
            // Link already exists — safe to ignore
        }
    }
}
