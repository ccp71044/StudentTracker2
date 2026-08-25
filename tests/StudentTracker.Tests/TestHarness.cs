using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;

namespace StudentTracker.Tests;

/// <summary>
/// Wires a full service graph over a throwaway SQLite database and provides shortcuts for the
/// fixtures nearly every test needs (a pool, a course, a delivery, a student).
/// </summary>
public sealed class TestHarness : IDisposable
{
    public StudentTrackerDbContext Context { get; }
    public CreditService Credits { get; }
    public BudgetService Budgets { get; }
    public AllocationService Allocations { get; }
    public CertificateService Certificates { get; }
    public ReportService Reports { get; }

    /// <summary>Isolated data root so document tests never touch the real profile.</summary>
    public string DataRoot { get; }

    public TestHarness()
    {
        Context = TestDbContextFactory.Create();
        DataRoot = Path.Combine(Path.GetTempPath(), "student-tracker-tests", Guid.NewGuid().ToString("N"));
        var settings = new AppSettings { DataRootPath = DataRoot };
        Context.AppSettings.Add(settings);
        Context.SaveChanges();

        var ids = new DisplayIdGenerator(Context);
        var audit = new AuditService(Context);
        Credits = new CreditService(Context, ids, audit);
        Budgets = new BudgetService(Context, ids, audit);
        Allocations = new AllocationService(Context, ids, audit, Credits, Budgets);
        Certificates = new CertificateService(Context, ids, Credits, audit);
        var documents = new DocumentService(Context, new DataLocationService(settings), ids, audit);
        Reports = new ReportService(Context, Credits, Budgets, documents);
    }

    public Task<CertificateCreditPool> CreditPoolAsync(string name = "Credits") =>
        Credits.CreatePoolAsync(new CertificateCreditPool { Name = name });

    public Task<BudgetPool> BudgetPoolAsync(string name = "Budget") =>
        Budgets.CreatePoolAsync(new BudgetPool { Name = name });

    public Student AddStudent(string first = "Test", string last = "Student")
    {
        var student = new Student { FirstName = first, LastName = last };
        Context.Students.Add(student);
        Context.SaveChanges();
        return student;
    }

    public CourseDelivery AddDelivery(decimal defaultCertificateCost = 100m)
    {
        var course = new CourseDefinition
        {
            CourseCode = $"C{Context.CourseDefinitions.Count() + 1}",
            CourseTitle = "Test Course",
            DefaultCertificateCost = defaultCertificateCost
        };
        Context.CourseDefinitions.Add(course);
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        Context.CourseDeliveries.Add(delivery);
        Context.SaveChanges();
        return delivery;
    }

    public OutcomeReason AddReason(string type, string name, bool requiresNotes = false)
    {
        var reason = new OutcomeReason { ReasonType = type, Name = name, RequiresNotes = requiresNotes };
        Context.OutcomeReasons.Add(reason);
        Context.SaveChanges();
        return reason;
    }

    public void Dispose()
    {
        Context.Dispose();
        if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, true);
    }
}
