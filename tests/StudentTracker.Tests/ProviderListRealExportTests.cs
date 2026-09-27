using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;
using Xunit.Abstractions;

namespace StudentTracker.Tests;

/// <summary>
/// Runs the provider's real student and course list exports end to end. Both name real people, so
/// they are not committed; see tests/README.md. Without them the tests report that they were
/// skipped and pass.
/// </summary>
public class ProviderListRealExportTests
{
    private const string StudentListFileName = "Student List (unique).xlsx";
    private const string CourseListFileName = "Course List - Completed Allens.xlsx";
    private const string StudentListVariable = "STUDENTTRACKER_STUDENT_LIST_WORKBOOK";
    private const string CourseListVariable = "STUDENTTRACKER_COURSE_LIST_WORKBOOK";

    private readonly ITestOutputHelper _output;

    public ProviderListRealExportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RealStudentList_ImportsEveryRowAndFlagsTheAmbiguousOnes()
    {
        var path = FindWorkbook(StudentListVariable, StudentListFileName);
        if (path is null)
        {
            _output.WriteLine($"Skipped: place '{StudentListFileName}' in testdata/ or set {StudentListVariable}.");
            return;
        }

        using var context = NewContext();
        var service = new ImportService(context, new DisplayIdGenerator(context), new AuditService(context));

        var result = await service.ImportMigrationPackageAsync(path);
        _output.WriteLine(result.Message);
        foreach (var review in context.ImportReviewQueues)
            _output.WriteLine($"  review: {review.Issue}");

        Assert.True(result.Success, result.Message);

        // 36 rows, one of which is the same provider number under a second client.
        Assert.Equal(35, context.Students.Count());
        Assert.All(context.Students, s => Assert.False(string.IsNullOrEmpty(s.ProviderStudentId)));
        Assert.Equal(35, context.Students.Select(s => s.ProviderStudentId).Distinct().Count());

        // Both work groups come through, and the student in both keeps the first and is queued.
        Assert.Contains(context.Students, s => s.WorkGroup == "T&C");
        Assert.Contains(context.Students, s => s.WorkGroup == "SCJV");
        Assert.Contains(context.ImportReviewQueues, r => r.Issue!.Contains("appears under both"));

        // Two rows have no surname, and the three near-identical "Prince" records must not be merged.
        Assert.Equal(2, context.Students.Count(s => s.LastName.Length == 0));
        Assert.Equal(3, context.Students.Count(s => s.FirstName == "Prince"));
        Assert.True(context.Students.Count(s => s.PotentialDuplicate) >= 3);
        Assert.All(context.ImportReviewQueues, r => Assert.Equal("Pending", r.Status));
    }

    [Fact]
    public async Task RealCourseList_ImportsEveryDeliveryAndReadsItsStartDate()
    {
        var path = FindWorkbook(CourseListVariable, CourseListFileName);
        if (path is null)
        {
            _output.WriteLine($"Skipped: place '{CourseListFileName}' in testdata/ or set {CourseListVariable}.");
            return;
        }

        using var context = NewContext();
        var service = new ImportService(context, new DisplayIdGenerator(context), new AuditService(context));

        var result = await service.ImportMigrationPackageAsync(path);
        _output.WriteLine(result.Message);
        foreach (var review in context.ImportReviewQueues)
            _output.WriteLine($"  review: {review.Issue}");

        Assert.True(result.Success, result.Message);
        Assert.Equal(35, context.CourseDeliveries.Count());
        Assert.All(context.CourseDeliveries, d => Assert.NotNull(d.StartDate));
        Assert.All(context.CourseDeliveries, d => Assert.Equal("Completed", d.DeliveryStatus));

        // Repeat deliveries of the same unit share one course definition.
        Assert.True(context.CourseDefinitions.Count() < context.CourseDeliveries.Count(),
            "Every delivery created its own course, so the course match key is not working.");
        Assert.Contains(context.CourseDefinitions, c => c.CourseCode == "HLTAID011");
        Assert.Contains(context.CourseDefinitions, c => c.CourseCode == "Course Set");
    }

    [Fact]
    public async Task RealExports_CombineIntoOneRegisterWithoutClashing()
    {
        var studentPath = FindWorkbook(StudentListVariable, StudentListFileName);
        var coursePath = FindWorkbook(CourseListVariable, CourseListFileName);
        if (studentPath is null || coursePath is null)
        {
            _output.WriteLine("Skipped: both provider exports are needed for this test.");
            return;
        }

        using var context = NewContext();
        var service = new ImportService(context, new DisplayIdGenerator(context), new AuditService(context));

        await service.ImportMigrationPackageAsync(studentPath);
        await service.ImportMigrationPackageAsync(coursePath);

        Assert.Equal(35, context.Students.Count());
        Assert.Equal(35, context.CourseDeliveries.Count());
        Assert.Equal(35, context.Students.Select(s => s.DisplayId).Distinct().Count());
        Assert.Equal(35, context.CourseDeliveries.Select(d => d.DisplayId).Distinct().Count());
    }

    private static StudentTracker.Data.StudentTrackerDbContext NewContext()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new AppSettings());
        context.SaveChanges();
        return context;
    }

    private static string? FindWorkbook(string environmentVariable, string fileName)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return File.Exists(fromEnvironment) ? fromEnvironment : null;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "testdata", fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
