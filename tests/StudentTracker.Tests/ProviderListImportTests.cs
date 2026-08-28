using ClosedXML.Excel;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

/// <summary>
/// The provider's student and course list exports, covered with workbooks built here so the quirks
/// of the real exports - blank surnames, "-" for an unknown date of birth, non-breaking spaces,
/// lower-case meridiems and truncated course names - are pinned down without shipping real names.
/// </summary>
public class ProviderListImportTests
{
    [Fact]
    public async Task StudentList_ImportsIdentityContactAndClient()
    {
        using var context = NewContext();
        var path = StudentWorkbook(
            ("3441694", "David", "Blizzard", new DateTime(1979, 8, 16), "david.blizzard@example.com", "T&C"));

        var result = await Import(context, path);

        Assert.True(result.Success, result.Message);
        var student = Assert.Single(context.Students);
        Assert.Equal("3441694", student.ProviderStudentId);
        Assert.Equal("Blizzard", student.LastName);
        Assert.Equal(new DateTime(1979, 8, 16), student.DateOfBirth);
        Assert.Equal("T&C", student.WorkGroup);
        Assert.StartsWith("STU-", student.DisplayId);
    }

    [Fact]
    public async Task StudentList_TreatsDashDateOfBirthAsUnknownRatherThanBadData()
    {
        using var context = NewContext();
        var path = StudentWorkbook(("3557011", "Tode", "Sitnikoski", "-", "todd@example.com", "T&C"));

        await Import(context, path);

        Assert.Null(context.Students.Single().DateOfBirth);
        Assert.Empty(context.ImportReviewQueues);
    }

    [Fact]
    public async Task StudentList_QueuesAnUnreadableDateOfBirth()
    {
        using var context = NewContext();
        var path = StudentWorkbook(("3557011", "Tode", "Sitnikoski", "not a date", "todd@example.com", "T&C"));

        await Import(context, path);

        Assert.Null(context.Students.Single().DateOfBirth);
        Assert.Contains(context.ImportReviewQueues, r => r.Issue!.Contains("not a date"));
    }

    [Fact]
    public async Task StudentList_ImportsAStudentWithNoSurnameAndQueuesIt()
    {
        using var context = NewContext();
        var path = StudentWorkbook(("3557039", "Jake", "", "-", "jake@example.com", "T&C"));

        await Import(context, path);

        var student = Assert.Single(context.Students);
        Assert.Equal("Jake", student.FirstName);
        Assert.Equal(string.Empty, student.LastName);
        var review = Assert.Single(context.ImportReviewQueues);
        Assert.Equal("Student", review.EntityType);
        Assert.Contains("no last name", review.Issue);
        Assert.Equal("Pending", review.Status);
    }

    [Fact]
    public async Task StudentList_KeepsOneRecordWhenTheSameStudentAppearsUnderTwoClients()
    {
        using var context = NewContext();
        var path = StudentWorkbook(
            ("3495571", "Garry", "Singleton", new DateTime(1971, 1, 6), "garry@example.com", "T&C"),
            ("3495571", "Garry", "Singleton", new DateTime(1971, 1, 6), "garry@example.com", "SCJV"));

        await Import(context, path);

        var student = Assert.Single(context.Students);
        Assert.Equal("T&C", student.WorkGroup);
        Assert.Contains(context.ImportReviewQueues, r => r.Issue!.Contains("both 'T&C' and 'SCJV'"));
    }

    [Fact]
    public async Task StudentList_FlagsNearIdenticalNamesWithoutMergingThem()
    {
        using var context = NewContext();
        var path = StudentWorkbook(
            ("3443634", "Prince", "Dalmeida", new DateTime(1985, 12, 17), "prince@example.com", "SCJV"),
            ("3470933", "Prince", "Delmeida", "-", "prince.delmeida@example.com", "SCJV"));

        await Import(context, path);

        Assert.Equal(2, context.Students.Count());
        Assert.All(context.Students, s => Assert.True(s.PotentialDuplicate));
        Assert.Contains(context.ImportReviewQueues, r => r.Issue!.Contains("closely matches"));
    }

    [Fact]
    public async Task StudentList_ReimportUpdatesRatherThanDuplicates()
    {
        using var context = NewContext();
        var path = StudentWorkbook(("3441694", "David", "Blizzard", new DateTime(1979, 8, 16), "david@example.com", "T&C"));

        await Import(context, path);
        var second = await Import(context, path);

        Assert.Single(context.Students);
        Assert.Contains("1 matched to existing", second.Message);
    }

    [Fact]
    public async Task CourseList_ImportsDeliveriesAndCreatesTheirCourses()
    {
        using var context = NewContext();
        var path = CourseWorkbook(
            ("1702281", "HLTAID009 Provide cardiopulmonary resuscitation", "01/05/2026 03:00pm", "\u00a0Completed"));

        var result = await Import(context, path);

        Assert.True(result.Success, result.Message);
        var delivery = Assert.Single(context.CourseDeliveries);
        Assert.Equal("1702281", delivery.ProviderCourseId);
        Assert.Equal(new DateTime(2026, 5, 1, 15, 0, 0), delivery.StartDate);
        Assert.Equal(DeliveryDateStatus.Confirmed, delivery.DateStatus);
        Assert.Equal("Completed", delivery.DeliveryStatus);

        var course = Assert.Single(context.CourseDefinitions);
        Assert.Equal("HLTAID009", course.CourseCode);
        Assert.Equal("Provide cardiopulmonary resuscitation", course.CourseTitle);
    }

    [Fact]
    public async Task CourseList_PutsDeliveriesOfTheSameCourseOnOneCourseDefinition()
    {
        using var context = NewContext();
        var path = CourseWorkbook(
            ("1701549", "HLTAID011 Provide First Aid", "29/05/2026 01:00pm", "Completed"),
            ("1701550", "HLTAID011 Provide First Aid", "05/06/2026 01:00pm", "Completed"));

        await Import(context, path);

        Assert.Equal(2, context.CourseDeliveries.Count());
        Assert.Single(context.CourseDefinitions);
    }

    [Fact]
    public async Task CourseList_QueuesTruncatedCourseSetsBecauseTheyCannotBeMatched()
    {
        using var context = NewContext();
        var path = CourseWorkbook(
            ("1765246", "Course Set RIIWHS202E & MSMWHS217 (Confined Spa...", "28/06/2026 07:30pm", "Completed"));

        await Import(context, path);

        Assert.Single(context.CourseDeliveries);
        var review = Assert.Single(context.ImportReviewQueues);
        Assert.Equal("CourseDelivery", review.EntityType);
        Assert.Contains("truncated", review.Issue);
    }

    [Fact]
    public async Task CourseList_LaterFullTitleReplacesAnEarlierTruncatedOne()
    {
        using var context = NewContext();
        await Import(context, CourseWorkbook(
            ("1704400", "PUAFIR306 Identify, detect and monitor hazardou...", "03/05/2026 10:00am", "Completed")));

        await Import(context, CourseWorkbook(
            ("1773863", "PUAFIR306 Identify, detect and monitor hazardous atmospheres", "21/07/2026 07:30am", "Completed")));

        var course = Assert.Single(context.CourseDefinitions);
        Assert.Equal("Identify, detect and monitor hazardous atmospheres", course.CourseTitle);
        Assert.Equal(2, context.CourseDeliveries.Count());
    }

    [Fact]
    public async Task CourseList_QueuesAnUnreadableStartDateAsTbc()
    {
        using var context = NewContext();
        var path = CourseWorkbook(("1702281", "HLTAID009 Provide cardiopulmonary resuscitation", "sometime in May", "Completed"));

        await Import(context, path);

        var delivery = Assert.Single(context.CourseDeliveries);
        Assert.Null(delivery.StartDate);
        Assert.Equal(DeliveryDateStatus.TBC, delivery.DateStatus);
        Assert.Contains(context.ImportReviewQueues, r => r.Issue!.Contains("sometime in May"));
    }

    [Fact]
    public async Task CourseList_ReimportUpdatesRatherThanDuplicates()
    {
        using var context = NewContext();
        var path = CourseWorkbook(("1702281", "HLTAID009 Provide cardiopulmonary resuscitation", "01/05/2026 03:00pm", "Completed"));

        await Import(context, path);
        await Import(context, path);

        Assert.Single(context.CourseDeliveries);
        Assert.Single(context.CourseDefinitions);
    }

    [Fact]
    public async Task StudentAndCourseWorkbooksAreRecognisedWithoutBeingTold()
    {
        using var context = NewContext();

        var students = await Import(context, StudentWorkbook(("3441694", "David", "Blizzard", "-", "david@example.com", "T&C")));
        var courses = await Import(context, CourseWorkbook(("1702281", "HLTAID009 Provide cardiopulmonary resuscitation", "01/05/2026 03:00pm", "Completed")));

        Assert.Contains("student list imported", students.Message);
        Assert.Contains("course list imported", courses.Message);
    }

    private static StudentTrackerDbContext NewContext()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new AppSettings());
        context.SaveChanges();
        return context;
    }

    private static Task<ImportResult> Import(StudentTrackerDbContext context, string xlsxPath)
    {
        var service = new ImportService(context, new DisplayIdGenerator(context), new AuditService(context));
        return service.ImportMigrationPackageAsync(xlsxPath);
    }

    /// <summary>Builds the provider's student export, non-breaking spaces in the headers included.</summary>
    private static string StudentWorkbook(params (string Id, string FirstName, string LastName, object Dob, string Email, string Client)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell(1, 1).Value = "ID\u00a0";
        sheet.Cell(1, 2).Value = "First name";
        sheet.Cell(1, 3).Value = "Last name";
        sheet.Cell(1, 4).Value = "Dob";
        sheet.Cell(1, 5).Value = "Email";
        sheet.Cell(1, 6).Value = "Client";

        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = rows[i].Id;
            sheet.Cell(row, 2).Value = rows[i].FirstName;
            sheet.Cell(row, 3).Value = rows[i].LastName;
            if (rows[i].Dob is DateTime date)
                sheet.Cell(row, 4).Value = date;
            else
                sheet.Cell(row, 4).Value = rows[i].Dob.ToString();
            sheet.Cell(row, 5).Value = rows[i].Email;
            sheet.Cell(row, 6).Value = rows[i].Client;
        }

        return Save(workbook);
    }

    private static string CourseWorkbook(params (string Id, string Type, string StartDate, string Status)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell(1, 1).Value = "Course ID\u00a0";
        sheet.Cell(1, 2).Value = "Course Type\u00a0";
        sheet.Cell(1, 3).Value = "Course Start Date\u00a0";
        sheet.Cell(1, 4).Value = "Process Status\u00a0";

        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = rows[i].Id;
            sheet.Cell(row, 2).Value = rows[i].Type;
            sheet.Cell(row, 3).Value = rows[i].StartDate;
            sheet.Cell(row, 4).Value = rows[i].Status;
        }

        return Save(workbook);
    }

    private static string Save(XLWorkbook workbook)
    {
        var directory = Path.Combine(Path.GetTempPath(), "StudentTrackerProviderLists", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "provider-export.xlsx");
        workbook.SaveAs(path);
        return path;
    }
}
