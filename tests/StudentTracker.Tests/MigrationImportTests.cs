using ClosedXML.Excel;
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
}
