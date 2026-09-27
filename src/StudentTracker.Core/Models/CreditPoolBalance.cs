namespace StudentTracker.Core.Models;

/// <summary>
/// Transaction-derived balance for a certificate credit pool. Every value is calculated
/// from <see cref="CertificateCreditTransaction"/> rows and is never stored or edited directly.
/// </summary>
public record CreditPoolBalance(
    decimal Loaded,
    decimal Adjustments,
    decimal Allocated,
    decimal Consumed,
    decimal Released,
    decimal Expired,
    decimal Unavailable)
{
    public decimal Available => Loaded + Adjustments - Allocated - Consumed - Expired - Unavailable;

    public static CreditPoolBalance Empty { get; } = new(0m, 0m, 0m, 0m, 0m, 0m, 0m);
}
