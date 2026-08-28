using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;
using Xunit.Abstractions;

namespace StudentTracker.Tests;

public class LegacyImportTests
{
    private readonly ITestOutputHelper _output;

    public LegacyImportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ImportLegacyStudentRegister_WorksWithRealWorkbook()
    {
        var workbookPath = FindWorkbook();
        if (workbookPath is null)
        {
            // The register holds real student data and is deliberately not committed. See tests/README.md.
            _output.WriteLine($"Skipped: no register workbook found. Set {WorkbookEnvironmentVariable} or place " +
                              $"'{WorkbookFileName}' in a 'testdata' folder at the repository root.");
            return;
        }

        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);

        var importService = new ImportService(context, gen, audit);
        var result = await importService.ImportMigrationPackageAsync(workbookPath);

        _output.WriteLine(result.Message);
        _output.WriteLine($"Students: {context.Students.Count()}");
        _output.WriteLine($"Courses: {context.CourseDefinitions.Count()}");
        _output.WriteLine($"Deliveries: {context.CourseDeliveries.Count()}");
        _output.WriteLine($"Allocations: {context.Allocations.Count()}");
        _output.WriteLine($"BudgetTransactions: {context.BudgetTransactions.Count()}");
        _output.WriteLine($"ReviewQueue: {context.ImportReviewQueues.Count()}");

        Assert.True(result.Success, result.Message);
        Assert.Equal(22, context.Students.Count());
        Assert.Equal(20, context.CourseDefinitions.Count());
        Assert.Equal(32, context.CourseDeliveries.Count());
        Assert.Equal(39, context.Allocations.Count());
        // 8 top-ups plus one commitment or expense per costed allocation.
        Assert.Equal(40, context.BudgetTransactions.Count());

        var scjv = context.BudgetPools.Single(p => p.Name == PoolNames.Scjv);
        var general = context.BudgetPools.Single(p => p.Name == PoolNames.General);
        _output.WriteLine($"SCJV transactions: {context.BudgetTransactions.Count(t => t.PoolId == scjv.Id)}");
        _output.WriteLine($"General transactions: {context.BudgetTransactions.Count(t => t.PoolId == general.Id)}");

        // The register's "SCJV n" tags must survive the import, otherwise the two pools cannot be told apart.
        Assert.Contains(context.Allocations, a => a.LegacyReference != null && a.LegacyReference.StartsWith(PoolNames.Scjv));
        Assert.All(context.Allocations, a => Assert.NotNull(a.BudgetPoolId));
        Assert.True(context.BudgetTransactions.Any(t => t.PoolId == scjv.Id), "No spending was attributed to the SCJV pool.");

        // Register rows that carry a cost but no course cannot become allocations and are queued for review.
        Assert.Equal(6, context.ImportReviewQueues.Count());
        Assert.All(context.ImportReviewQueues, r => Assert.Equal("Pending", r.Status));
    }

    private const string WorkbookFileName = "Student Tracker.xlsx";
    private const string WorkbookEnvironmentVariable = "STUDENTTRACKER_REGISTER_WORKBOOK";

    private static string? FindWorkbook()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(WorkbookEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return File.Exists(fromEnvironment) ? fromEnvironment : null;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "testdata", WorkbookFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
