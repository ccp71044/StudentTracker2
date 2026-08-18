using StudentTracker.Core.Models;

namespace StudentTracker.Services;

public class DataLocationService
{
    private readonly AppSettings _settings;

    public DataLocationService(AppSettings settings)
    {
        _settings = settings;
    }

    public string DataRoot => _settings.DataRootPath;
    public string DatabasePath => Path.Combine(DataRoot, "Database", "student-tracker.db");
    public string DocumentsPath => Path.Combine(DataRoot, "Documents");
    public string ImportsPath => Path.Combine(DataRoot, "Imports");
    public string ExportsPath => Path.Combine(DataRoot, "Exports");
    public string IntegrationPath => Path.Combine(DataRoot, "Integration");
    public string BackupsPath => _settings.BackupLocation;
    public string LogsPath => Path.Combine(DataRoot, "Logs");
    public string TemplatesPath => Path.Combine(DataRoot, "Templates");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(DataRoot, "Database"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "Students"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "Courses"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "CourseDeliveries"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "SignOffs"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "Certificates"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "Invoices"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "Reports"));
        Directory.CreateDirectory(Path.Combine(DocumentsPath, "General"));
        Directory.CreateDirectory(ImportsPath);
        Directory.CreateDirectory(ExportsPath);
        Directory.CreateDirectory(Path.Combine(IntegrationPath, "InvoicerImport"));
        Directory.CreateDirectory(Path.Combine(IntegrationPath, "InvoicerExport"));
        Directory.CreateDirectory(Path.Combine(IntegrationPath, "Processed"));
        Directory.CreateDirectory(Path.Combine(IntegrationPath, "Errors"));
        Directory.CreateDirectory(BackupsPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(TemplatesPath);
    }

    public string GetConnectionString() => $"Data Source={DatabasePath};Foreign Keys=True";
}
