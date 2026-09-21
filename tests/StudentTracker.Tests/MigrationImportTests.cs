using ClosedXML.Excel;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class MigrationImportTests
{
    [Fact]
    public void ImportWorkbook_ImportsStudentsAndCourses()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);

        var path = Path.Combine(Path.GetTempPath(), $"migration-test-{Guid.NewGuid()}.xlsx");
        using (var wb = new XLWorkbook())
        {
            var studentsSheet = wb.Worksheets.Add("Students");
            studentsSheet.Cell(1, 1).Value = "FirstName";
            studentsSheet.Cell(1, 2).Value = "LastName";
            studentsSheet.Cell(1, 3).Value = "Email";
            studentsSheet.Cell(2, 1).Value = "Jane";
            studentsSheet.Cell(2, 2).Value = "Doe";
            studentsSheet.Cell(2, 3).Value = "jane.doe@example.com";

            var coursesSheet = wb.Worksheets.Add("CourseDefinitions");
            coursesSheet.Cell(1, 1).Value = "CourseCode";
            coursesSheet.Cell(1, 2).Value = "CourseTitle";
            coursesSheet.Cell(1, 3).Value = "Provider";
            coursesSheet.Cell(2, 1).Value = "HLTAID011";
            coursesSheet.Cell(2, 2).Value = "Provide First Aid";
            coursesSheet.Cell(2, 3).Value = "Allied First Aid";

            wb.SaveAs(path);
        }

        try
        {
            var importer = new MigrationPackageImporter(context, gen, audit);
            var result = importer.ImportWorkbook(path);

            Assert.True(result.Success);
            Assert.Equal(2, context.Students.Count() + context.CourseDefinitions.Count());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportWorkbook_ImportsCanonicalWorkbookAndRelationships()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var path = Path.Combine(Path.GetTempPath(), $"migration-test-{Guid.NewGuid()}.xlsx");

        using (var workbook = new XLWorkbook())
        {
            AddSheet(workbook, "Students",
                ["DisplayId", "FirstName", "LastName", "Manager", "GroupTag", "IsActive", "IsArchived"],
                ["STU-100", "Jane", "Doe", "Pat Manager", "Cohort A", true, false]);
            AddSheet(workbook, "CourseDefinitions",
                ["CourseCode", "CourseTitle", "DefaultCertificateCost", "CourseDurationDays", "IsActive"],
                ["HLTAID011", "Provide First Aid", 25m, 1, true]);
            AddSheet(workbook, "CourseDeliveries",
                ["CourseCode", "DisplayId", "StartDate", "EndDate", "DateStatus", "DeliveryStatus", "Notes"],
                ["HLTAID011", "DEL-100", new DateTime(2026, 9, 18), new DateTime(2026, 9, 18), "Confirmed", "Completed", "Imported delivery"]);
            AddSheet(workbook, "BudgetPools",
                ["Name", "FinancialPeriod", "Notes", "IsActive"],
                ["Annual Budget", "2026", "Imported budget", true]);
            AddSheet(workbook, "CertificateCreditPools",
                ["Name", "Provider", "UnitType", "ExpiryDate", "Notes", "IsActive"],
                ["Default", "Provider", "Monetary", new DateTime(2027, 6, 30), "Imported credits", true]);
            AddSheet(workbook, "Allocations",
                ["StudentDisplayId", "DeliveryDisplayId", "AllocationStatus", "AttendanceStatus", "OutcomeStatus", "OutcomeDate", "CertificateCost", "BudgetPoolName", "CreditPoolName"],
                ["STU-100", "DEL-100", "Finalised", "Attended", "Completed", new DateTime(2026, 9, 18), 25m, "Annual Budget", "Default"]);
            workbook.SaveAs(path);
        }

        try
        {
            var importer = new MigrationPackageImporter(context, new DisplayIdGenerator(context), new AuditService(context));
            var result = importer.ImportWorkbook(path);

            Assert.True(result.Success);
            Assert.Empty(importer.ReviewQueue);
            var student = Assert.Single(context.Students);
            var course = Assert.Single(context.CourseDefinitions);
            var delivery = Assert.Single(context.CourseDeliveries);
            var budgetPool = Assert.Single(context.BudgetPools);
            var creditPool = Assert.Single(context.CertificateCreditPools);
            var allocation = Assert.Single(context.Allocations);
            Assert.Equal("STU-100", student.DisplayId);
            Assert.Equal("Pat Manager", student.Manager);
            Assert.Equal("Cohort A", student.GroupTag);
            Assert.Equal("HLTAID011", course.CourseCode);
            Assert.Equal(1, course.CourseDurationDays);
            Assert.Equal("DEL-100", delivery.DisplayId);
            Assert.Equal(course.Id, delivery.CourseDefinitionId);
            Assert.Equal("Completed", delivery.DeliveryStatus);
            Assert.Equal("Imported delivery", delivery.Notes);
            Assert.Equal(student.Id, allocation.StudentId);
            Assert.Equal(delivery.Id, allocation.CourseDeliveryId);
            Assert.Equal(budgetPool.Id, allocation.BudgetPoolId);
            Assert.Equal(creditPool.Id, allocation.CreditPoolId);
            Assert.Equal(AllocationStatus.Finalised, allocation.AllocationStatus);
            Assert.Equal(AttendanceStatus.Attended, allocation.AttendanceStatus);
            Assert.Equal(OutcomeStatus.Completed, allocation.OutcomeStatus);
            Assert.Equal(new DateTime(2027, 6, 30), creditPool.ExpiryDate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AddSheet(XLWorkbook workbook, string name, object[] headers, object[] values)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = XLCellValue.FromObject(headers[column]);
            sheet.Cell(2, column + 1).Value = XLCellValue.FromObject(values[column]);
        }
    }
}
