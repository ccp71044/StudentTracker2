namespace StudentTracker.Core.Models;

/// <summary>
/// Well-known pool names. Spending is attributed to a client pool (SCJV) or to general
/// business spending; the provider pool mirrors the training provider's credit account.
/// </summary>
public static class PoolNames
{
    public const string Scjv = "SCJV";
    public const string General = "General";
    public const string ProviderCredit = "Allens Training Credit";
}
