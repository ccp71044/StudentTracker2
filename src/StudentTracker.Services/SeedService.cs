using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class SeedService
{
    private readonly StudentTrackerDbContext _context;

    public SeedService(StudentTrackerDbContext context)
    {
        _context = context;
    }

    public async Task SeedOutcomeReasonsAsync()
    {
        if (await _context.OutcomeReasons.AnyAsync()) return;

        int order = 0;
        foreach (var name in new[] { "Student request", "Employer request", "Medical", "Scheduling conflict", "No longer employed", "Prerequisite not met", "Transferred", "Duplicate allocation", "Administrative error", "Insufficient notice to reallocate", "Other" })
        {
            _context.OutcomeReasons.Add(new OutcomeReason
            {
                ReasonType = "Withdrawal",
                Name = name,
                RequiresNotes = name == "Other" || name == "Administrative error" || name == "Insufficient notice to reallocate",
                SortOrder = order++
            });
        }

        order = 0;
        foreach (var name in new[] { "Did not attend", "Left early", "Assessment not completed", "Assessment unsuccessful", "Online learning not completed", "Prerequisite not met", "Medical", "Administrative", "Other" })
        {
            _context.OutcomeReasons.Add(new OutcomeReason
            {
                ReasonType = "NonCompletion",
                Name = name,
                RequiresNotes = name == "Other" || name == "Administrative",
                SortOrder = order++
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task SeedSampleDataAsync(DisplayIdGenerator idGenerator)
    {
        if (await _context.Students.AnyAsync()) return;

        var firstAid = new CourseDefinition
        {
            CourseCode = "HLTAID011",
            CourseTitle = "Provide First Aid",
            Category = "First Aid",
            Provider = "Allied First Aid",
            DefaultCertificateCost = 25m,
            DefaultCreditQuantity = 1
        };
        var cpr = new CourseDefinition
        {
            CourseCode = "HLTAID009",
            CourseTitle = "Provide cardiopulmonary resuscitation",
            Category = "First Aid",
            Provider = "Allied First Aid",
            DefaultCertificateCost = 18m,
            DefaultCreditQuantity = 1
        };
        _context.CourseDefinitions.AddRange(firstAid, cpr);
        await _context.SaveChangesAsync();

        var creditPool = new CertificateCreditPool
        {
            Name = "Allied First Aid Credits",
            Provider = "Allied First Aid",
            UnitType = CreditUnitType.Count,
            Notes = "Default certificate credit pool"
        };
        _context.CertificateCreditPools.Add(creditPool);

        var budgetPool = new BudgetPool
        {
            Name = "General Training Budget",
            FinancialPeriod = "2024/25"
        };
        _context.BudgetPools.Add(budgetPool);
        await _context.SaveChangesAsync();

        _context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
        {
            DisplayId = idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
            PoolId = creditPool.Id,
            TransactionType = CreditTransactionType.TopUp,
            Amount = 20m,
            Quantity = 20m,
            Reason = "Initial sample top-up",
            SourceType = CreditSourceType.Manual,
            TransactionDateTime = DateTime.UtcNow
        });

        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = budgetPool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = 5000m,
            Reason = "Initial sample funds",
            TransactionDate = DateTime.UtcNow
        });

        var student = new Student
        {
            DisplayId = idGenerator.NextStudentId(),
            FirstName = "Alex",
            LastName = "Sample",
            Email = "alex.sample@example.com",
            Employer = "Sample Employer"
        };
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var delivery = new CourseDelivery
        {
            DisplayId = idGenerator.NextDisplayId<CourseDelivery>("DEL"),
            CourseDefinitionId = firstAid.Id,
            StartDate = DateTime.Today.AddDays(7),
            DateStatus = DeliveryDateStatus.Confirmed,
            Location = "Training Room A",
            TrainerName = "J. Smith",
            Capacity = 12
        };
        _context.CourseDeliveries.Add(delivery);
        await _context.SaveChangesAsync();
    }
}
