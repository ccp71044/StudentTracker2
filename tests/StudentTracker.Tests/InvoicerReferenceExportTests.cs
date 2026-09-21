using System.Text.Json;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class InvoicerReferenceExportTests : IDisposable
{
    private readonly string _testDir;

    public InvoicerReferenceExportTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"st-invoicer-ref-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static StudentTrackerDbContext CreateContext()
    {
        var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new AppSettings());
        context.SaveChanges();
        return context;
    }

    private static InvoicerReferenceExportService CreateService(StudentTrackerDbContext context, string dataRoot)
    {
        var settings = new AppSettings { DataRootPath = dataRoot };
        var location = new DataLocationService(settings);
        location.EnsureDirectories();
        var pricing = new PricingService(context);
        var gen = new DisplayIdGenerator(context);
        var audit = new AuditService(context);
        var entitlement = new ClientPrepaidEntitlementService(context, gen, audit);
        return new InvoicerReferenceExportService(context, location, pricing, entitlement, audit);
    }

    [Fact]
    public async Task ExportCostPositionSnapshot_WritesJsonAndCsv_AndRecordsAudit()
    {
        using var context = CreateContext();
        var service = CreateService(context, _testDir);

        var result = await service.ExportCostPositionSnapshotAsync("test-notes");

        Assert.NotEqual(Guid.Empty, result.SnapshotId);
        Assert.True(File.Exists(result.JsonPath), "JSON file should be written");
        Assert.True(File.Exists(result.CsvPath), "CSV file should be written");
        Assert.EndsWith(".json", Path.GetFileName(result.JsonPath), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".csv", Path.GetFileName(result.CsvPath), StringComparison.OrdinalIgnoreCase);

        var json = await File.ReadAllTextAsync(result.JsonPath);
        var snapshot = JsonSerializer.Deserialize<InvoicerCostPositionSnapshot>(json);
        Assert.NotNull(snapshot);
        Assert.Equal("1.0", snapshot.SchemaVersion);
        Assert.Equal(result.SnapshotId, snapshot.SnapshotId);
        Assert.Equal("test-notes", snapshot.Notes);
        Assert.Equal("StudentTracker", snapshot.SourceApplication);

        var audit = context.AuditLogs.SingleOrDefault(a => a.Action == "Exported" && a.EntityType == "InvoicerCostPositionSnapshot");
        Assert.NotNull(audit);
        Assert.Equal(result.SnapshotId.ToString("N")[..8], audit.EntityDisplayId);
    }

    [Fact]
    public async Task ExportCostPositionSnapshot_ComputesPoolAndCoursePositions()
    {
        using var context = CreateContext();
        var service = CreateService(context, _testDir);

        var pool = new BudgetPool { Name = "Test Pool", FinancialPeriod = "2026" };
        context.BudgetPools.Add(pool);
        await context.SaveChangesAsync();

        context.BudgetTransactions.Add(new BudgetTransaction
        {
            PoolId = pool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = 1000m,
            TransactionDate = DateTime.UtcNow
        });

        var course = new CourseDefinition
        {
            CourseCode = "C-101",
            CourseTitle = "Test Course",
            Provider = "Allen Training",
            DefaultCertificateCost = 100m,
            IsActive = true
        };
        context.CourseDefinitions.Add(course);
        await context.SaveChangesAsync();

        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);

        var enrolledStudent = new Student { FirstName = "Enrolled", LastName = "Student" };
        var completedStudent = new Student { FirstName = "Completed", LastName = "Student" };
        var spentStudent = new Student { FirstName = "Spent", LastName = "Student" };
        context.Students.AddRange(enrolledStudent, completedStudent, spentStudent);
        await context.SaveChangesAsync();

        context.Allocations.AddRange(
            new Allocation
            {
                CourseDeliveryId = delivery.Id,
                StudentId = enrolledStudent.Id,
                BudgetPoolId = pool.Id,
                AllocationStatus = AllocationStatus.Enrolled,
                OutcomeStatus = OutcomeStatus.Pending,
                CashCommitmentStatus = CashCommitmentStatus.Pending,
                CertificateCost = 100m
            },
            new Allocation
            {
                CourseDeliveryId = delivery.Id,
                StudentId = completedStudent.Id,
                BudgetPoolId = pool.Id,
                AllocationStatus = AllocationStatus.Finalised,
                OutcomeStatus = OutcomeStatus.Completed,
                CashCommitmentStatus = CashCommitmentStatus.Pending,
                CertificateCost = 100m
            },
            new Allocation
            {
                CourseDeliveryId = delivery.Id,
                StudentId = spentStudent.Id,
                BudgetPoolId = pool.Id,
                AllocationStatus = AllocationStatus.Finalised,
                OutcomeStatus = OutcomeStatus.Completed,
                CashCommitmentStatus = CashCommitmentStatus.Spent,
                CertificateCost = 100m
            },
            new Allocation
            {
                CourseDeliveryId = delivery.Id,
                BudgetPoolId = pool.Id,
                PlaceholderName = "Reserved Place",
                AllocationStatus = AllocationStatus.Reserved,
                OutcomeStatus = OutcomeStatus.Pending,
                CashCommitmentStatus = CashCommitmentStatus.None,
                CertificateCost = 100m
            });
        await context.SaveChangesAsync();

        var pendingAllocations = context.Allocations.Where(a => a.CashCommitmentStatus == CashCommitmentStatus.Pending).ToList();
        var spentAllocation = context.Allocations.Single(a => a.CashCommitmentStatus == CashCommitmentStatus.Spent);
        context.BudgetTransactions.AddRange(pendingAllocations.Select(a => new BudgetTransaction
        {
            PoolId = pool.Id,
            AllocationId = a.Id,
            TransactionType = BudgetTransactionType.CommitmentCreated,
            Amount = -100m,
            TransactionDate = DateTime.UtcNow
        }));
        context.BudgetTransactions.Add(new BudgetTransaction
        {
            PoolId = pool.Id,
            AllocationId = spentAllocation.Id,
            TransactionType = BudgetTransactionType.ExpenseRecognised,
            Amount = -100m,
            TransactionDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await service.ExportCostPositionSnapshotAsync();
        var snapshot = JsonSerializer.Deserialize<InvoicerCostPositionSnapshot>(await File.ReadAllTextAsync(result.JsonPath));
        Assert.NotNull(snapshot);

        var poolPosition = Assert.Single(snapshot.Pools);
        Assert.Equal(pool.Id, poolPosition.PoolId);
        Assert.Equal("Test Pool", poolPosition.PoolName);
        Assert.Equal(1000m, poolPosition.FundsAdded);
        Assert.Equal(200m, poolPosition.Committed); // enrolled pending + completed pending
        Assert.Equal(100m, poolPosition.Spent);     // completed spent
        Assert.Equal(700m, poolPosition.Available); // 1000 - 200 - 100
        Assert.Equal(1, poolPosition.AnonymousReservedPlaces);
        Assert.Equal(1, poolPosition.AssignedPending);
        Assert.Equal(1, poolPosition.CompletedAwaitingManualSpend);
        Assert.Equal(7, poolPosition.CompletionsRemaining); // floor(700 / 100)

        var coursePosition = Assert.Single(poolPosition.Courses);
        Assert.Equal(course.Id, coursePosition.CourseId);
        Assert.Equal("C-101", coursePosition.CourseCode);
        Assert.Equal("Test Course", coursePosition.CourseTitle);
        Assert.Equal("Allen Training", coursePosition.Provider);
        Assert.Equal(1000m, coursePosition.Funds);
        Assert.Equal(200m, coursePosition.Committed);
        Assert.Equal(100m, coursePosition.Spent);
        Assert.Equal(700m, coursePosition.Available);
        Assert.Equal(1, coursePosition.AnonymousReservedPlaces);
        Assert.Equal(1, coursePosition.AssignedPending);
        Assert.Equal(1, coursePosition.CompletedAwaitingManualSpend);
        Assert.Equal(7, coursePosition.CompletionsRemaining);
        Assert.Equal(100m, coursePosition.ProviderCost);
        Assert.Equal(4, coursePosition.TotalAllocations);
    }

    [Fact]
    public async Task ExportCostPositionSnapshot_HonoursDuplicateSafeFileNames()
    {
        using var context = CreateContext();
        var service = CreateService(context, _testDir);

        var result1 = await service.ExportCostPositionSnapshotAsync();
        var result2 = await service.ExportCostPositionSnapshotAsync();

        Assert.NotEqual(result1.JsonPath, result2.JsonPath);
        Assert.NotEqual(result1.CsvPath, result2.CsvPath);
        Assert.True(File.Exists(result1.JsonPath));
        Assert.True(File.Exists(result2.JsonPath));
    }

    [Fact]
    public async Task ExportCostPositionSnapshot_IncludesStableIdsInOutput()
    {
        using var context = CreateContext();
        var service = CreateService(context, _testDir);

        var pool = new BudgetPool { Name = "Stable Pool" };
        context.BudgetPools.Add(pool);
        var course = new CourseDefinition { CourseCode = "S-101", CourseTitle = "Stable Course", IsActive = true };
        context.CourseDefinitions.Add(course);
        await context.SaveChangesAsync();

        context.BudgetTransactions.Add(new BudgetTransaction
        {
            PoolId = pool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = 500m
        });
        var delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
        var student = new Student { FirstName = "S", LastName = "T" };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        context.Allocations.Add(new Allocation
        {
            CourseDeliveryId = delivery.Id,
            StudentId = student.Id,
            BudgetPoolId = pool.Id,
            CashCommitmentStatus = CashCommitmentStatus.Pending,
            CertificateCost = 50m
        });
        await context.SaveChangesAsync();

        var result = await service.ExportCostPositionSnapshotAsync();
        var snapshot = JsonSerializer.Deserialize<InvoicerCostPositionSnapshot>(await File.ReadAllTextAsync(result.JsonPath));
        Assert.NotNull(snapshot);

        Assert.Equal(pool.Id, snapshot.Pools[0].PoolId);
        Assert.Equal(course.Id, snapshot.Pools[0].Courses[0].CourseId);
    }
}
