using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class CourseKeyTests
{
    [Theory]
    [InlineData("HLTAID011 Provide First Aid", "HLTAID011", "Provide First Aid")]
    [InlineData("RIIWHS202E Enter and work in confined spaces", "RIIWHS202E", "Enter and work in confined spaces")]
    [InlineData("11244NAT Course in Mental Health Support", "11244NAT", "Course in Mental Health Support")]
    [InlineData("22578VIC Course in First Aid Management of Anaphylaxis", "22578VIC", "Course in First Aid Management of Anaphylaxis")]
    public void Split_SeparatesLeadingCodeFromTitle(string input, string expectedCode, string expectedTitle)
    {
        var (code, title) = CourseKey.Split(input);
        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void Split_TreatsCourseSetsAsASingleOffering()
    {
        var (code, _) = CourseKey.Split("Course Set HLTAID014 & HLTAID015");
        Assert.Equal("Course Set", code);
    }

    [Fact]
    public void Build_IgnoresPunctuationAndCaseSoTheSameSetMatchesAcrossFiles()
    {
        Assert.Equal(
            CourseKey.Build("Course Set - HLTAID014 & HLTAID015"),
            CourseKey.Build("Course Set HLTAID014 and HLTAID015".Replace(" and ", " & ")));
    }

    [Fact]
    public void Build_NormalisesCodeCasing()
    {
        Assert.Equal(CourseKey.Build("hltaid011 provide first aid"), CourseKey.Build("HLTAID011 Provide First Aid"));
    }
}

public class CompletionPricingImportTests
{
    private const string PriceCsv = """
        Course Type,completion_price (AU$)
        HLTAID011 Provide First Aid,20.00
        HLTAID009 Provide cardiopulmonary resuscitation,9.00
        "Course Set HLTAID014, HLTAID015",31.50
        RIIWHS202E Enter and work in confined spaces,not a price
        """;

    [Fact]
    public void Import_ReadsQuotedDescriptionsAndQueuesBadPrices()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();
        var importer = NewImporter(context);

        var result = importer.Import(new StringReader(PriceCsv), "prices.csv");

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, context.CoursePrices.Count());
        // The quoted description contains a comma and must not be split into two fields.
        Assert.Contains(context.CourseDefinitions, c => c.CourseTitle.Contains("HLTAID015"));
        Assert.Single(importer.ReviewQueue);
    }

    [Fact]
    public void Import_IsIdempotentWhenPricesAreUnchanged()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();

        NewImporter(context).Import(new StringReader(PriceCsv), "prices.csv");
        var afterFirst = context.CoursePrices.Count();

        NewImporter(context).Import(new StringReader(PriceCsv), "prices.csv");

        Assert.Equal(afterFirst, context.CoursePrices.Count());
        Assert.Equal(3, context.CourseDefinitions.Count());
    }

    [Fact]
    public async Task PricingService_ReturnsThePriceInForceOnAGivenDate()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "Provide First Aid" };
        context.CourseDefinitions.Add(course);
        context.SaveChanges();

        var pricing = new PricingService(context);
        await pricing.SetPriceAsync(course.Id, 18m, new DateTime(2025, 1, 1));
        await pricing.SetPriceAsync(course.Id, 20m, new DateTime(2026, 1, 1));

        Assert.Equal(18m, await pricing.GetPriceAsync(course.Id, new DateTime(2025, 6, 1)));
        Assert.Equal(20m, await pricing.GetPriceAsync(course.Id, new DateTime(2026, 6, 1)));
    }

    private static CompletionPricingImporter NewImporter(Data.StudentTrackerDbContext context) =>
        new(context, new DisplayIdGenerator(context), new AuditService(context));
}

public class ProviderCreditHistoryImportTests
{
    private const string HistoryCsv = """
        id,date_and_time,descriptor,extra_details,credit,debit,user
        101,27/07/2026 01:12pm,Credit purchase,Ref ABC123,55.50,,operator
        102,28/07/2026 09:00am,Course #1761277,3 x HLTAID011 - Provide First Aid,,60.00,operator
        103,28/07/2026 10:00am,Course #1761278,1 x HLTAID009 - Provide CPR,,9.00,operator
        """;

    [Fact]
    public void Import_SplitsPurchasesFromConsumptionsAndKeepsProviderReferences()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();

        var result = NewImporter(context).Import(new StringReader(HistoryCsv), "history.csv");

        Assert.True(result.Success, result.Message);
        var transactions = context.CertificateCreditTransactions.ToList();
        Assert.Equal(3, transactions.Count);

        var purchase = transactions.Single(t => t.TransactionType == CreditTransactionType.TopUp);
        Assert.Equal(55.50m, purchase.Amount);
        Assert.Equal("101", purchase.ExternalTransactionId);

        var firstAid = transactions.Single(t => t.ExternalTransactionId == "102");
        Assert.Equal(3m, firstAid.Quantity);
        Assert.Equal("1761277", firstAid.ExternalCourseNumber);
        Assert.All(transactions, t => Assert.Equal(CreditSourceType.ProviderHistory, t.SourceType));
    }

    [Fact]
    public void Import_SkipsRowsAlreadyPresentSoALongerExportOnlyAddsNewOnes()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        context.SaveChanges();

        NewImporter(context).Import(new StringReader(HistoryCsv), "history.csv");
        var extended = HistoryCsv + "\n104,29/07/2026 11:00am,Credit purchase,Ref DEF456,100.00,,operator";
        var second = NewImporter(context).Import(new StringReader(extended), "history.csv");

        Assert.Equal(4, context.CertificateCreditTransactions.Count());
        Assert.Contains("3 already present", second.Message);
    }

    private static ProviderCreditHistoryImporter NewImporter(Data.StudentTrackerDbContext context) =>
        new(context, new DisplayIdGenerator(context), new AuditService(context));
}

public class BudgetSummaryTests
{
    [Fact]
    public async Task PoolSummaries_SeparateSpentFromCommittedPerPool()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());

        var scjv = new BudgetPool { Name = PoolNames.Scjv };
        var general = new BudgetPool { Name = PoolNames.General };
        context.BudgetPools.AddRange(scjv, general);
        context.BudgetTransactions.AddRange(
            new BudgetTransaction { PoolId = scjv.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 500m },
            new BudgetTransaction { PoolId = scjv.Id, TransactionType = BudgetTransactionType.ExpenseRecognised, Amount = -120m },
            new BudgetTransaction { PoolId = scjv.Id, TransactionType = BudgetTransactionType.CommitmentCreated, Amount = -80m },
            new BudgetTransaction { PoolId = general.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 100m });
        context.SaveChanges();

        var summaries = await new BudgetSummaryService(context, new PricingService(context)).GetPoolSummariesAsync();
        var scjvSummary = summaries.Single(s => s.Name == PoolNames.Scjv);

        Assert.Equal(120m, scjvSummary.Spent);
        Assert.Equal(80m, scjvSummary.Committed);
        Assert.Equal(380m, scjvSummary.Balance);
        Assert.Equal(300m, scjvSummary.Free);
    }

    [Fact]
    public async Task CompletionsRemaining_DividesTheFreeBalanceByTheCurrentPrice()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());

        var pool = new BudgetPool { Name = PoolNames.General };
        context.BudgetPools.Add(pool);
        context.BudgetTransactions.Add(new BudgetTransaction
        {
            PoolId = pool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = 290m
        });

        var course = new CourseDefinition { CourseCode = "HLTAID011", CourseTitle = "Provide First Aid" };
        context.CourseDefinitions.Add(course);
        context.SaveChanges();

        var pricing = new PricingService(context);
        await pricing.SetPriceAsync(course.Id, 20m, DateTime.UtcNow.AddDays(-1));

        var remaining = await new BudgetSummaryService(context, pricing).GetCompletionsRemainingAsync();

        Assert.Equal(14, remaining.Single().Remaining);
    }

    [Fact]
    public async Task PoolSummaries_ReportPrepaidPlacePositionsWithoutCombiningPools()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        var first = new BudgetPool { Name = "First" };
        var second = new BudgetPool { Name = "Second" };
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course" };
        var delivery = new CourseDelivery { CourseDefinition = course };
        context.AddRange(first, second, delivery);
        context.Allocations.AddRange(
            new Allocation { CourseDelivery = delivery, BudgetPool = first, PlaceholderName = "Place 1", AllocationStatus = AllocationStatus.Reserved },
            new Allocation { CourseDelivery = delivery, BudgetPool = first, Student = new Student { FirstName = "A", LastName = "B" }, OutcomeStatus = OutcomeStatus.Pending },
            new Allocation { CourseDelivery = delivery, BudgetPool = first, Student = new Student { FirstName = "C", LastName = "D" }, OutcomeStatus = OutcomeStatus.Completed, CashCommitmentStatus = CashCommitmentStatus.Pending },
            new Allocation { CourseDelivery = delivery, BudgetPool = second, PlaceholderName = "Place 2", AllocationStatus = AllocationStatus.Reserved });
        context.SaveChanges();

        var summaries = await new BudgetSummaryService(context, new PricingService(context)).GetPoolSummariesAsync();

        var firstSummary = summaries.Single(s => s.PoolId == first.Id);
        Assert.Equal(1, firstSummary.UnassignedPlaceholderPlaces);
        Assert.Equal(1, firstSummary.AssignedPendingPlaces);
        Assert.Equal(1, firstSummary.CompletedAwaitingManualSpend);
        Assert.Equal(1, summaries.Single(s => s.PoolId == second.Id).UnassignedPlaceholderPlaces);
    }

    [Fact]
    public async Task CompletionsRemaining_ReportsEachPoolSeparately()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());
        var first = new BudgetPool { Name = "First" };
        var second = new BudgetPool { Name = "Second" };
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course", DefaultCertificateCost = 20m };
        context.AddRange(first, second, course);
        context.BudgetTransactions.AddRange(
            new BudgetTransaction { PoolId = first.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 100m },
            new BudgetTransaction { PoolId = second.Id, TransactionType = BudgetTransactionType.FundsAdded, Amount = 60m });
        context.SaveChanges();

        var rows = await new BudgetSummaryService(context, new PricingService(context)).GetCompletionsRemainingAsync();

        Assert.Equal(5, rows.Single(r => r.PoolId == first.Id).Remaining);
        Assert.Equal(3, rows.Single(r => r.PoolId == second.Id).Remaining);
    }

    [Fact]
    public async Task Reconciliation_FlagsTheDollarDifferenceInsteadOfOverwritingTheRegister()
    {
        using var context = TestDbContextFactory.Create();
        context.AppSettings.Add(new());

        var pool = new BudgetPool { Name = PoolNames.General };
        var creditPool = new CertificateCreditPool { Name = PoolNames.ProviderCredit };
        context.BudgetPools.Add(pool);
        context.CertificateCreditPools.Add(creditPool);

        var date = new DateTime(2026, 7, 27);
        context.BudgetTransactions.Add(new BudgetTransaction
        {
            PoolId = pool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = 55m,
            TransactionDate = date
        });
        context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
        {
            PoolId = creditPool.Id,
            TransactionType = CreditTransactionType.TopUp,
            SourceType = CreditSourceType.ProviderHistory,
            Amount = 55.50m,
            TransactionDateTime = date
        });
        context.SaveChanges();

        var result = await new BudgetSummaryService(context, new PricingService(context)).ReconcileTopUpsAsync();

        Assert.False(result.IsBalanced);
        Assert.Equal(0.50m, result.Difference);
        var discrepancy = Assert.Single(result.Discrepancies);
        Assert.Equal(55m, discrepancy.RegisterAmount);
        Assert.Equal(55.50m, discrepancy.ProviderAmount);
    }
}
