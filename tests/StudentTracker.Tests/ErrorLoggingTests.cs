using System.Text;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using StudentTracker.Services;

namespace StudentTracker.Tests;

/// <summary>Captures Serilog output so tests can assert that failures are recorded.</summary>
public sealed class CapturingSink : ILogEventSink, IDisposable
{
    private readonly ILogger _previous = Log.Logger;
    private readonly List<LogEvent> _events = new();

    public CapturingSink()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_events) _events.Add(logEvent);
    }

    public IReadOnlyList<LogEvent> Errors
    {
        get { lock (_events) return _events.Where(e => e.Level >= LogEventLevel.Error).ToList(); }
    }

    public void Dispose() => Log.Logger = _previous;
}

[CollectionDefinition("SerilogGlobal", DisableParallelization = true)]
public class SerilogGlobalCollection { }

/// <summary>
/// These tests swap the global Serilog logger, so they share a collection with every other test
/// class that logs, to keep captured events deterministic.
/// </summary>
[Collection("SerilogGlobal")]
public class ErrorLoggingTests
{
    [Fact]
    public void RestoringAMissingBackup_IsLoggedWithTheFailingOperation()
    {
        using var log = new CapturingSink();
        using var harness = new TestHarness();

        Assert.Throws<FileNotFoundException>(() => harness.Backups.RestoreBackup(
            Path.Combine(harness.DataRoot, "does-not-exist.zip")));

        var error = Assert.Single(log.Errors);
        Assert.Equal("RestoreBackup failed", error.RenderMessage().Replace("\"", string.Empty));
        Assert.IsType<FileNotFoundException>(error.Exception);
    }

    [Fact]
    public void BackingUpWithoutADatabase_IsLogged()
    {
        using var log = new CapturingSink();
        using var harness = new TestHarness();

        Assert.Throws<InvalidOperationException>(() => harness.Backups.CreateBackup("manual"));

        Assert.Contains(log.Errors, e => e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task AMalformedWorkbook_IsReportedAndLoggedRatherThanThrown()
    {
        using var log = new CapturingSink();
        using var harness = new TestHarness();
        Directory.CreateDirectory(harness.DataRoot);
        var path = Path.Combine(harness.DataRoot, "not-a-workbook.xlsx");
        await File.WriteAllTextAsync(path, "this is not a spreadsheet");

        var imports = new ImportService(harness.Context, new DisplayIdGenerator(harness.Context), new AuditService(harness.Context));
        var result = await imports.ImportMigrationPackageAsync(path);

        Assert.False(result.Success);
        Assert.Contains("could not be imported", result.Message);
        Assert.Contains(log.Errors, e => e.Exception != null);
    }

    [Fact]
    public async Task AMalformedCsv_IsReportedAndLoggedRatherThanThrown()
    {
        using var log = new CapturingSink();
        using var harness = new TestHarness();

        var imports = new ImportService(harness.Context, new DisplayIdGenerator(harness.Context), new AuditService(harness.Context));
        // Wrong file picked in the file dialog: nothing should be imported from it.
        var csv = new MemoryStream(Encoding.UTF8.GetBytes("nothing,useful\n1,2\n"));
        var result = await imports.ImportCsvAsync("CreditHistory", csv);

        Assert.False(result.Success);
        Assert.Contains("missing column", result.Message);
        Assert.Empty(harness.Context.CertificateCreditTransactions);
        Assert.Contains(log.Errors, e => e.Exception != null);
    }

    [Fact]
    public async Task ASuccessfulOperation_LogsNoErrors()
    {
        using var log = new CapturingSink();
        using var harness = new TestHarness();

        var pool = await harness.CreditPoolAsync("Provider");
        await harness.Credits.TopUpAsync(pool.Id, 10m, 10m);

        Assert.Empty(log.Errors);
    }
}
