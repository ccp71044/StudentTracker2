using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public sealed class DataCutoverTests
{
    [Fact]
    public async Task InvalidWorkbook_IsRejectedBeforeDatabaseChanges()
    {
        using var fixture = new CutoverFixture();
        fixture.Context.Students.Add(new Student { DisplayId = "OLD-1", FirstName = "Old", LastName = "Student" });
        fixture.Context.SaveChanges();
        var workbook = fixture.CreateWorkbook(brokenStudentReference: true);

        var preview = await fixture.Service.PreviewAsync(workbook);
        var result = await fixture.Service.ExecuteAsync(preview, DataCutoverService.ConfirmationPhrase);

        Assert.False(preview.IsValid);
        Assert.Contains(preview.Errors, e => e.Contains("Broken allocation student"));
        Assert.False(result.Success);
        Assert.Equal("OLD-1", Assert.Single(fixture.Context.Students).DisplayId);
        Assert.Empty(Directory.GetFiles(fixture.Settings.BackupLocation));
    }

    [Fact]
    public async Task ConfirmedCutover_ReplacesAtomically_PreservesSettingsAndDocumentFile_AndCreatesBackups()
    {
        using var fixture = new CutoverFixture();
        var settingsId = fixture.Settings.Id;
        fixture.Context.Students.Add(new Student { DisplayId = "OLD-1", FirstName = "Old", LastName = "Student" });
        fixture.Context.SaveChanges();
        var documentPath = Path.Combine(fixture.Settings.DataRootPath, "Documents", "keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        File.WriteAllText(documentPath, "must remain");
        var workbook = fixture.CreateWorkbook();

        var preview = await fixture.Service.PreviewAsync(workbook);
        var refused = await fixture.Service.ExecuteAsync(preview, "replace data");
        Assert.False(refused.Success);
        Assert.Equal("OLD-1", Assert.Single(fixture.Context.Students).DisplayId);

        var result = await fixture.Service.ExecuteAsync(preview, DataCutoverService.ConfirmationPhrase);

        Assert.True(result.Success, result.Message);
        Assert.Equal("STU-100", Assert.Single(fixture.Context.Students).DisplayId);
        Assert.Single(fixture.Context.CourseDefinitions);
        Assert.Single(fixture.Context.CourseDeliveries);
        Assert.Single(fixture.Context.Allocations);
        Assert.Equal(settingsId, Assert.Single(fixture.Context.AppSettings).Id);
        Assert.True(File.Exists(documentPath));
        Assert.True(File.Exists(result.PreCutoverBackup));
        Assert.True(File.Exists(result.PostCutoverBackup));
        Assert.Contains(fixture.Context.AuditLogs, a => a.Action == "DataCutoverCompleted");
    }

    private sealed class CutoverFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "student-tracker-cutover-test-" + Guid.NewGuid());
        public AppSettings Settings { get; }
        public StudentTrackerDbContext Context { get; }
        public DataCutoverService Service { get; }

        public CutoverFixture()
        {
            Settings = new AppSettings { DataRootPath = _root, BackupLocation = Path.Combine(_root, "Backups") };
            var location = new DataLocationService(Settings);
            location.EnsureDirectories();
            Context = new StudentTrackerDbContext(new DbContextOptionsBuilder<StudentTrackerDbContext>().UseSqlite(location.GetConnectionString()).Options);
            Context.Database.EnsureCreated();
            Context.AppSettings.Add(Settings);
            Context.SaveChanges();
            var audit = new AuditService(Context);
            Service = new DataCutoverService(Context, new BackupService(location, Context, audit), new DisplayIdGenerator(Context), audit);
        }

        public string CreateWorkbook(bool brokenStudentReference = false)
        {
            var path = Path.Combine(_root, Guid.NewGuid() + ".xlsx");
            using var wb = new XLWorkbook();
            Add(wb, "Students", ["DisplayId", "FirstName", "LastName"], ["STU-100", "Jane", "Doe"]);
            Add(wb, "CourseDefinitions", ["CourseCode", "CourseTitle"], ["COURSE-1", "Course One"]);
            Add(wb, "CourseDeliveries", ["CourseCode", "DisplayId", "DateStatus", "DeliveryStatus"], ["COURSE-1", "DEL-100", "Confirmed", "Scheduled"]);
            Add(wb, "Allocations", ["StudentDisplayId", "DeliveryDisplayId", "AllocationStatus"], [brokenStudentReference ? "MISSING" : "STU-100", "DEL-100", "Enrolled"]);
            wb.SaveAs(path);
            return path;
        }

        private static void Add(XLWorkbook wb, string name, object[] headers, object[] values)
        {
            var ws = wb.Worksheets.Add(name);
            for (var i = 0; i < headers.Length; i++) { ws.Cell(1, i + 1).Value = XLCellValue.FromObject(headers[i]); ws.Cell(2, i + 1).Value = XLCellValue.FromObject(values[i]); }
        }

        public void Dispose()
        {
            Context.Dispose();
            try { Directory.Delete(_root, true); } catch { }
        }
    }
}
