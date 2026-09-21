using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;

namespace StudentTracker.Tests;

/// <summary>
/// Builds services whose dependency graph is not the subject of the test, using a
/// throwaway data root so document writes never touch a real install.
/// </summary>
internal static class TestServiceFactory
{
    public static DocumentService Documents(StudentTrackerDbContext context, DisplayIdGenerator ids, AuditService audit)
    {
        var settings = context.AppSettings.FirstOrDefault() ?? new AppSettings();
        if (string.IsNullOrWhiteSpace(settings.DataRootPath))
            settings.DataRootPath = Path.Combine(Path.GetTempPath(), "student-tracker-tests", Guid.NewGuid().ToString("N"));
        return new DocumentService(context, new DataLocationService(settings), ids, audit);
    }

    public static CreditService Credits(StudentTrackerDbContext context, DisplayIdGenerator ids, AuditService audit) =>
        new(context, ids, audit, Documents(context, ids, audit));
}
