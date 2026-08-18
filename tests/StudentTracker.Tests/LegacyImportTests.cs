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
        Assert.True(File.Exists(workbookPath), $"Workbook not found at {workbookPath}");

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
        Assert.Equal(20, context.Students.Count());
        Assert.Equal(18, context.CourseDefinitions.Count());
        Assert.Equal(29, context.CourseDeliveries.Count());
        Assert.Equal(36, context.Allocations.Count());
        Assert.Equal(8, context.BudgetTransactions.Count());
        Assert.True(!context.ImportReviewQueues.Any() || context.ImportReviewQueues.All(r => r.Status == "Pending"), "Unexpected review queue state.");
    }

    private static string FindWorkbook()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Student Tracker.xlsx")))
        {
            dir = dir.Parent;
        }
        return dir != null ? Path.Combine(dir.FullName, "Student Tracker.xlsx") : string.Empty;
    }
}
