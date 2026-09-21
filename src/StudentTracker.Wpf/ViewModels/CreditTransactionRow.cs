using StudentTracker.Core.Models;

namespace StudentTracker.Wpf.ViewModels;

public class CreditTransactionRow
{
    public CertificateCreditTransaction Transaction { get; set; } = null!;
    public Document? Receipt { get; set; }
    public string ReceiptDisplayName => Receipt?.DisplayName ?? string.Empty;
}
