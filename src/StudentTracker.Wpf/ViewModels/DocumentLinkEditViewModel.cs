using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DocumentLinkEditViewModel : ViewModelBase, ICloseable
{
    private readonly DocumentService _service;
    private readonly Document _document;

    public event Action<bool?>? RequestClose;
    public IReadOnlyList<string> EntityTypes { get; } = ["Student", "Allocation", "CourseDelivery", "CertificateOrder", "CertificateDelivery"];

    [ObservableProperty] private string _selectedEntityType = "Student";
    [ObservableProperty] private ObservableCollection<DocumentLinkTarget> _targets = new();
    [ObservableProperty] private DocumentLinkTarget? _selectedTarget;
    [ObservableProperty] private string? _purpose;

    public DocumentLinkEditViewModel(Document document, DocumentService service)
    {
        _document = document;
        _service = service;
        LoadTargetsAsync().ConfigureAwait(false);
    }

    partial void OnSelectedEntityTypeChanged(string value) => LoadTargetsAsync().ConfigureAwait(false);

    private async Task LoadTargetsAsync()
    {
        Targets = new ObservableCollection<DocumentLinkTarget>(await _service.GetLinkTargetsAsync(SelectedEntityType));
        SelectedTarget = null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedTarget == null) return;
        await _service.LinkDocumentAsync(_document.Id, SelectedEntityType, SelectedTarget.Id, Purpose);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
