using StudentTracker.Core.Enums;
using StudentTracker.Services;
using Xunit.Abstractions;

namespace StudentTracker.Tests;

/// <summary>
/// Runs the provider credit history importer over a real export. The export names staff and is
/// deliberately not committed: drop it in a 'testdata' folder at the repository root, or point
/// STUDENTTRACKER_CREDIT_HISTORY_CSV at it. See tests/README.md.
/// </summary>
public class ProviderCreditHistoryRealExportTests
{
    private const string FilePattern = "credit-transaction-history-*.csv";
    private const string EnvironmentVariable = "STUDENTTRACKER_CREDIT_HISTORY_CSV";

    private readonly ITestOutputHelper _output;

    public ProviderCreditHistoryRealExportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Import_ReproducesTheProviderAccountBalance()
    {
        var path = FindExport();
        if (path is null)
        {
            _output.WriteLine($"Skipped: no export found. Set {EnvironmentVariable} or place a file matching " +
                              $"'{FilePattern}' in a 'testdata' folder at the repository root.");
            return;
        }

        using var harness = new TestHarness();
        var importer = new ProviderCreditHistoryImporter(
            harness.Context,
            new DisplayIdGenerator(harness.Context),
            new AuditService(harness.Context));

        var result = importer.Import(path);
        _output.WriteLine(result.Message);

        Assert.True(result.Success, result.Message);

        // Every row must land as a transaction: an unparsed row silently loses money from the ledger.
        var dataRows = File.ReadAllLines(path).Skip(1).Count(l => !string.IsNullOrWhiteSpace(l));
        var transactions = harness.Context.CertificateCreditTransactions.ToList();
        Assert.Equal(dataRows, transactions.Count);
        Assert.All(transactions, t => Assert.NotEqual(default, t.TransactionDateTime));
        Assert.All(transactions, t => Assert.True(t.Amount > 0m, $"Row {t.ExternalTransactionId} imported a non-positive amount."));

        // Consumptions must carry the provider's course number so they can be matched to a delivery.
        var consumed = transactions.Where(t => t.TransactionType == CreditTransactionType.ManualConsume).ToList();
        Assert.All(consumed, t => Assert.NotNull(t.ExternalCourseNumber));

        // The derived balance must equal credits minus debits in the export.
        var expected = File.ReadLines(path).Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Sum(_ => 0m)
            + transactions.Where(t => t.TransactionType == CreditTransactionType.TopUp).Sum(t => t.Amount)
            - consumed.Sum(t => t.Amount);

        var pool = harness.Context.CertificateCreditPools.Single();
        var balance = await harness.Credits.GetBalanceAsync(pool.Id);
        _output.WriteLine($"Loaded {balance.Loaded}, consumed {balance.Consumed}, available {balance.Available}");

        Assert.Equal(expected, balance.Available);
    }

    private static string? FindExport()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return File.Exists(fromEnvironment) ? fromEnvironment : null;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var testData = Path.Combine(dir.FullName, "testdata");
            if (!Directory.Exists(testData)) continue;
            var match = Directory.GetFiles(testData, FilePattern).OrderBy(f => f).LastOrDefault();
            if (match != null) return match;
        }

        return null;
    }
}
