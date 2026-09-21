using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;

namespace StudentTracker.Data;

public class StudentTrackerDbContext : DbContext
{
    public StudentTrackerDbContext(DbContextOptions<StudentTrackerDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<CourseDefinition> CourseDefinitions => Set<CourseDefinition>();
    public DbSet<CoursePrice> CoursePrices => Set<CoursePrice>();
    public DbSet<CourseDelivery> CourseDeliveries => Set<CourseDelivery>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<OutcomeReason> OutcomeReasons => Set<OutcomeReason>();
    public DbSet<CertificateCreditPool> CertificateCreditPools => Set<CertificateCreditPool>();
    public DbSet<CertificateCreditTransaction> CertificateCreditTransactions => Set<CertificateCreditTransaction>();
    public DbSet<BudgetPool> BudgetPools => Set<BudgetPool>();
    public DbSet<BudgetTransaction> BudgetTransactions => Set<BudgetTransaction>();
    public DbSet<ClientPrepaidPool> ClientPrepaidPools => Set<ClientPrepaidPool>();
    public DbSet<ClientPrepaidEntitlementTransaction> ClientPrepaidEntitlementTransactions => Set<ClientPrepaidEntitlementTransaction>();
    public DbSet<FundingSource> FundingSources => Set<FundingSource>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<CertificateOrder> CertificateOrders => Set<CertificateOrder>();
    public DbSet<CertificateDelivery> CertificateDeliveries => Set<CertificateDelivery>();
    public DbSet<SignOff> SignOffs => Set<SignOff>();
    public DbSet<SignOffParticipant> SignOffParticipants => Set<SignOffParticipant>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<ExportBatch> ExportBatches => Set<ExportBatch>();
    public DbSet<ExportBatchItem> ExportBatchItems => Set<ExportBatchItem>();
    public DbSet<ImportReviewQueue> ImportReviewQueues => Set<ImportReviewQueue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>().Property(s => s.PotentialDuplicate).HasDefaultValue(false);
        modelBuilder.Entity<Allocation>().Property(a => a.AllocationStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.AttendanceStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.OutcomeStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.CreditStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.CertificateOrderStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.CertificateDeliveryStatus).HasConversion<string>();
        modelBuilder.Entity<Allocation>().Property(a => a.CashCommitmentStatus).HasConversion<string>();
        modelBuilder.Entity<CertificateCreditTransaction>().Property(t => t.TransactionType).HasConversion<string>();
        modelBuilder.Entity<BudgetPool>().Property(p => p.Category).HasConversion<string>();
        modelBuilder.Entity<BudgetTransaction>().Property(t => t.TransactionType).HasConversion<string>();
        modelBuilder.Entity<SignOff>().Property(s => s.Status).HasConversion<string>();
        modelBuilder.Entity<Document>().Property(d => d.Status).HasConversion<string>();
        modelBuilder.Entity<CertificateOrder>().Property(o => o.Status).HasConversion<string>();
        modelBuilder.Entity<CourseDelivery>().Property(d => d.DateStatus).HasConversion<string>();
        modelBuilder.Entity<FundingSource>().Property(f => f.Type).HasConversion<string>();
        modelBuilder.Entity<CertificateCreditPool>().Property(p => p.UnitType).HasConversion<string>();
        modelBuilder.Entity<CertificateCreditTransaction>().Property(t => t.SourceType).HasConversion<string>();
        modelBuilder.Entity<CoursePrice>().Property(p => p.SourceType).HasConversion<string>();
        modelBuilder.Entity<ClientPrepaidEntitlementTransaction>().Property(t => t.TransactionType).HasConversion<string>();

        modelBuilder.Entity<ClientPrepaidEntitlementTransaction>()
            .HasOne(t => t.LinkedTransaction)
            .WithMany()
            .HasForeignKey(t => t.LinkedTransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ClientPrepaidPool>()
            .HasOne(p => p.RestrictedToCourseDefinition)
            .WithMany()
            .HasForeignKey(p => p.RestrictedToCourseDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Allocation>()
            .HasOne(a => a.ClientPrepaidPool)
            .WithMany()
            .HasForeignKey(a => a.ClientPrepaidPoolId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Allocation>()
            .HasOne(a => a.ClientPrepaidEntitlementTransaction)
            .WithMany()
            .HasForeignKey(a => a.ClientPrepaidEntitlementTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Allocation>().HasIndex(a => new { a.StudentId, a.CourseDeliveryId });
        modelBuilder.Entity<DocumentLink>().HasIndex(l => new { l.EntityType, l.EntityId });
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.EntityType);
        modelBuilder.Entity<CourseDefinition>().HasIndex(c => c.MatchKey);
        modelBuilder.Entity<CoursePrice>().HasIndex(p => new { p.CourseDefinitionId, p.EffectiveFrom });
        modelBuilder.Entity<CertificateCreditTransaction>().HasIndex(t => t.ExternalTransactionId);
    }
}
