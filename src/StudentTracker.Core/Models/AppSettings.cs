namespace StudentTracker.Core.Models;

public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = "Student Tracker";
    public string? CompanyLogoPath { get; set; }
    public string? DefaultTrainerName { get; set; }
    public string? DefaultTrainerBusinessDetails { get; set; }
    public string? DefaultAuthorisedByName { get; set; }
    public string? DefaultAuthorisedByPosition { get; set; }
    public string? DefaultVerifiedByName { get; set; }
    public string? DefaultVerifiedByPosition { get; set; }
    public string? SignOffDeclarationText { get; set; }
    public string BillableTrigger { get; set; } = "Ordered";
    public string ExpenseTrigger { get; set; } = "Completion";
    public Guid? DefaultCreditPoolId { get; set; }
    public Guid? DefaultBudgetPoolId { get; set; }
    public string DataRootPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudentTracker");
    public string BackupLocation { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudentTracker", "Backups");
    public string InvoicerExchangeLocation { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudentTracker", "Integration", "InvoicerExport");
    public string? ReportFooter { get; set; }
    public string Currency { get; set; } = "AUD";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public int StudentIdSeed { get; set; } = 1;
    public int DeliveryIdSeed { get; set; } = 1;
}
