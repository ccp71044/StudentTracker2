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
        Assert.Equal(22, context.Students.Count());
        Assert.Equal(20, context.CourseDefinitions.Count());
        Assert.Equal(32, context.CourseDeliveries.Count());
        Assert.Equal(39, context.Allocations.Count());
        Assert.Equal(8, context.BudgetTransactions.Count());

        // Register rows that carry a cost but no course cannot become allocations and are queued for review.
        Assert.Equal(6, context.ImportReviewQueues.Count());
        Assert.All(context.ImportReviewQueues, r => Assert.Equal("Pending", r.Status));
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
