using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;

namespace StudentTracker.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task GetAwaitingOrderReportAsync_ReturnsReadyAndNotReadyCompleted()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation
        {
            StudentId = student.Id,
            CourseDeliveryId = delivery.Id,
            OutcomeStatus = OutcomeStatus.Completed,
            CertificateOrderStatus = CertificateOrderStatus.Ready,
            CertificateCost = 50
        });
        context.Allocations.Add(new Allocation
        {
            StudentId = student.Id,
            CourseDeliveryId = delivery.Id,
            OutcomeStatus = OutcomeStatus.Pending,
            CertificateOrderStatus = CertificateOrderStatus.Ready
        });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetAwaitingOrderReportAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetCapacityReportAsync_IncludesEnrolledCount()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        delivery.Capacity = 10;
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id });
        context.Allocations.Add(new Allocation { CourseDeliveryId = delivery.Id, PlaceholderName = "Hold" });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCapacityReportAsync();

        Assert.Single(result);
        Assert.Equal(2, result[0].EnrolledCount);
        Assert.Equal(8, result[0].AvailablePlaces);
    }

    [Fact]
    public async Task GetActiveAndCancelledAndPlaceholderAllocationsAsync_FilterByStatus()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, AllocationStatus = AllocationStatus.Active });
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, AllocationStatus = AllocationStatus.Cancelled });
        context.Allocations.Add(new Allocation { CourseDeliveryId = delivery.Id, PlaceholderName = "Team A" });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        Assert.Single(await service.GetActiveAllocationsAsync());
        Assert.Single(await service.GetCancelledAllocationsAsync());
        Assert.Single(await service.GetPlaceholderAllocationsAsync());
    }

    [Fact]
    public async Task GetAttendanceReportAsync_OnlyRecordsNonDefaultAttendance()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, AttendanceStatus = AttendanceStatus.Attended });
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, AttendanceStatus = AttendanceStatus.NotRecorded });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetAttendanceReportAsync();

        Assert.Single(result);
        Assert.Equal("Attended", result[0].AttendanceStatus);
    }

    [Fact]
    public async Task GetCourseUtilizationReportAsync_AggregatesByCourse()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, OutcomeStatus = OutcomeStatus.Completed, CertificateCost = 25, BudgetPoolId = null });
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, OutcomeStatus = OutcomeStatus.Withdrawn });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCourseUtilizationReportAsync();

        Assert.Single(result);
        Assert.Equal(2, result[0].TotalAllocations);
        Assert.Equal(1, result[0].Completed);
        Assert.Equal(1, result[0].Withdrawn);
        Assert.Equal(25m, result[0].TotalCertificateCost);
    }

    [Fact]
    public async Task GetBudgetTransactionSummaryAsync_GroupsByPoolAndType()
    {
        using var context = TestDbContextFactory.Create();
        var pool = new BudgetPool { Name = "B1" };
        context.BudgetPools.Add(pool);
        context.BudgetTransactions.Add(new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 100 });
        context.BudgetTransactions.Add(new BudgetTransaction { PoolId = pool.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 200 });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetBudgetTransactionSummaryAsync();

        Assert.Single(result);
        Assert.Equal(2, result[0].Count);
        Assert.Equal(300m, result[0].TotalAmount);
    }

    [Fact]
    public async Task GetCreditTransactionSummaryAsync_GroupsByPoolAndType()
    {
        using var context = TestDbContextFactory.Create();
        var pool = new CertificateCreditPool { Name = "C1" };
        context.CertificateCreditPools.Add(pool);
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = pool.Id, TransactionType = CreditTransactionType.TopUp, Amount = 10, Quantity = 1 });
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = pool.Id, TransactionType = CreditTransactionType.TopUp, Amount = 20, Quantity = 2 });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCreditTransactionSummaryAsync();

        Assert.Single(result);
        Assert.Equal(2, result[0].Count);
        Assert.Equal(30m, result[0].TotalAmount);
        Assert.Equal(3m, result[0].TotalQuantity);
    }

    [Fact]
    public async Task GetCertificateOrderReportAsync_IncludesTurnaroundForDelivered()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        var alloc = new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id };
        context.Allocations.Add(alloc);
        context.SaveChanges();

        var order = new CertificateOrder
        {
            AllocationId = alloc.Id,
            OrderedDate = DateTime.UtcNow.AddDays(-5),
            Status = CertificateOrderStatus.Ordered,
            Provider = "Provider A"
        };
        context.CertificateOrders.Add(order);
        context.SaveChanges();

        context.CertificateDeliveries.Add(new CertificateDelivery
        {
            CertificateOrderId = order.Id,
            DeliveredDate = DateTime.UtcNow
        });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCertificateOrderReportAsync();

        Assert.Single(result);
        Assert.NotNull(result[0].TurnaroundDays);
        Assert.True(result[0].TurnaroundDays >= 4.9);
    }

    [Fact]
    public async Task GetImportReviewQueueReportAsync_DefaultsToPending()
    {
        using var context = TestDbContextFactory.Create();
        context.ImportReviewQueues.Add(new ImportReviewQueue { SourceFileName = "f.xlsx", SourceSheet = "s", SourceRow = 1, EntityType = "Student", ProposedAction = "Create", Status = "Pending" });
        context.ImportReviewQueues.Add(new ImportReviewQueue { SourceFileName = "f.xlsx", SourceSheet = "s", SourceRow = 2, EntityType = "Student", ProposedAction = "Create", Status = "Approved" });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetImportReviewQueueReportAsync();

        Assert.Single(result);
        Assert.Equal(1, result[0].SourceRow);
    }

    [Fact]
    public async Task GetAuditActivityReportAsync_FiltersByDateRange()
    {
        using var context = TestDbContextFactory.Create();
        context.AuditLogs.Add(new AuditLog { Timestamp = DateTime.UtcNow.AddDays(-2), Action = "Create", EntityType = "Student" });
        context.AuditLogs.Add(new AuditLog { Timestamp = DateTime.UtcNow, Action = "Update", EntityType = "Student" });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetAuditActivityReportAsync(DateTime.UtcNow.AddDays(-1), null);

        Assert.Single(result);
        Assert.Equal("Update", result[0].Action);
    }

    [Fact]
    public async Task WF009_ImportReviewQueue_ResolvePending()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var item = new ImportReviewQueue
        {
            SourceFileName = "import.xlsx",
            SourceSheet = "Students",
            SourceRow = 1,
            EntityType = "Student",
            ProposedAction = "Create",
            Issue = "Possible duplicate",
            Status = "Pending"
        };
        context.ImportReviewQueues.Add(item);
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var pending = await service.GetImportReviewQueueReportAsync("Pending");
        Assert.Single(pending);

        item.Status = "Approved";
        item.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var approved = await service.GetImportReviewQueueReportAsync("Approved");
        Assert.Single(approved);
        Assert.Empty(await service.GetImportReviewQueueReportAsync("Pending"));
    }

    [Fact]
    public async Task ExportCsvAsync_WritesBytesForReportItems()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var items = new List<AwaitingOrderReportItem>
        {
            new() { StudentName = "A", CourseCode = "C1", CertificateOrderStatus = "Ready", CashCommitmentStatus = "Pending" }
        };

        var bytes = await service.ExportCsvAsync(items);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("A", text);
        Assert.Contains("C1", text);
    }

    [Fact]
    public async Task GetBillableCertificatesForInvoicerAsync_ReturnsBillableOnly()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, IsBillable = true, CertificateCost = 120 });
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, IsBillable = false, CertificateCost = 120 });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetBillableCertificatesForInvoicerAsync();

        Assert.Single(result);
        Assert.Equal(120m, result[0].CertificateCost);
        Assert.False(result[0].IsExported);
    }

    [Fact]
    public async Task GetTbcCourseDeliveriesAsync_ReturnsTbcOnly()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out _, out var delivery);
        context.CourseDeliveries.Add(new CourseDelivery { CourseDefinitionId = delivery.CourseDefinitionId, DeliveryStatus = "TBC", DisplayId = "DEL-TBC" });
        context.CourseDeliveries.Add(new CourseDelivery { CourseDefinitionId = delivery.CourseDefinitionId, DeliveryStatus = "Scheduled", DisplayId = "DEL-SCH" });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetTbcCourseDeliveriesAsync();

        Assert.Single(result);
        Assert.Equal("TBC", result[0].DeliveryStatus);
    }

    [Fact]
    public async Task GetFundingSourcesAsync_GroupsBySource()
    {
        using var context = TestDbContextFactory.Create();
        var pool = new BudgetPool { Name = "B1" };
        context.BudgetPools.Add(pool);
        var source = new FundingSource { Name = "Grant A" };
        context.FundingSources.Add(source);
        context.BudgetTransactions.Add(new BudgetTransaction { PoolId = pool.Id, FundingSourceId = source.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 500 });
        context.BudgetTransactions.Add(new BudgetTransaction { PoolId = pool.Id, FundingSourceId = source.Id, TransactionType = BudgetTransactionType.ExpenseRecognised, Amount = -100 });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetFundingSourcesAsync();

        Assert.Single(result);
        Assert.Equal(500m, result[0].TotalIn);
        Assert.Equal(100m, result[0].TotalOut);
        Assert.Equal(400m, result[0].Net);
    }

    [Fact]
    public async Task GetMissingDocumentsAsync_ReturnsCompletedAllocationsWithoutDocumentLinks()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        context.Allocations.Add(new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, OutcomeStatus = OutcomeStatus.Completed });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetMissingDocumentsAsync();

        Assert.Single(result);
        Assert.Equal("Completion Evidence", result[0].MissingDocumentType);
    }

    [Fact]
    public async Task GetCertificateCreditPoolSummaryAsync_SummarizesEachPool()
    {
        using var context = TestDbContextFactory.Create();
        var pool = new CertificateCreditPool { Name = "C1" };
        context.CertificateCreditPools.Add(pool);
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = pool.Id, TransactionType = CreditTransactionType.TopUp, Amount = 100, Quantity = 1 });
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = pool.Id, TransactionType = CreditTransactionType.Allocate, Amount = -30, Quantity = 1 });
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = pool.Id, TransactionType = CreditTransactionType.OrderConsume, Amount = -20, Quantity = 1 });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCertificateCreditPoolSummaryAsync();

        Assert.Single(result);
        Assert.Equal(100m, result[0].TopUp);
        Assert.Equal(30m, result[0].Allocated);
        Assert.Equal(20m, result[0].Consumed);
        Assert.Equal(50m, result[0].Available);
    }

    [Fact]
    public async Task GetCreditsConsumedWithoutCompletionAsync_ReturnsConsumedForNonCompleted()
    {
        using var context = TestDbContextFactory.Create();
        SeedStudentAndCourse(context, out var student, out var delivery);
        var alloc = new Allocation { StudentId = student.Id, CourseDeliveryId = delivery.Id, OutcomeStatus = OutcomeStatus.Withdrawn };
        context.Allocations.Add(alloc);
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction { PoolId = Guid.NewGuid(), AllocationId = alloc.Id, TransactionType = CreditTransactionType.OrderConsume, Amount = -25, Quantity = 1, TransactionDateTime = DateTime.UtcNow });
        context.SaveChanges();

        var service = new ReportService(context, new BudgetSummaryService(context, new PricingService(context)), new PricingService(context));
        var result = await service.GetCreditsConsumedWithoutCompletionAsync();

        Assert.Single(result);
        Assert.Equal(25m, result[0].AmountConsumed);
        Assert.Equal("Withdrawn", result[0].OutcomeStatus);
    }

    private static void SeedStudentAndCourse(StudentTrackerDbContext context, out Student student, out CourseDelivery delivery)
    {
        student = new Student { FirstName = "A", LastName = "B" };
        context.Students.Add(student);
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course 1" };
        context.CourseDefinitions.Add(course);
        delivery = new CourseDelivery { CourseDefinitionId = course.Id };
        context.CourseDeliveries.Add(delivery);
    }
}
