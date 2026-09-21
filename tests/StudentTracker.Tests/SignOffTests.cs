using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using Xunit;

namespace StudentTracker.Tests;

public class SignOffTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static (StudentTracker.Data.StudentTrackerDbContext ctx, SignOffService svc, AuditService audit) CreateServices()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.AppSettings.Add(new AppSettings
        {
            DefaultAuthorisedByName = "Auth Person",
            DefaultAuthorisedByPosition = "Manager",
            DefaultVerifiedByName = "Verify Person",
            DefaultVerifiedByPosition = "Director"
        });
        ctx.SaveChanges();
        var gen = new DisplayIdGenerator(ctx);
        var audit = new AuditService(ctx);
        var svc = new SignOffService(ctx, gen, audit);
        return (ctx, svc, audit);
    }

    private static (CourseDelivery delivery, Allocation alloc1, Allocation alloc2) SeedDeliveryWithAllocations(StudentTracker.Data.StudentTrackerDbContext ctx)
    {
        var course = new CourseDefinition { CourseCode = "C1", CourseTitle = "Course One" };
        ctx.CourseDefinitions.Add(course);

        var delivery = new CourseDelivery
        {
            CourseDefinitionId = course.Id,
            TrainerName = "Trainer One",
            TrainerBusinessDetails = "Business Details",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = new DateTime(2026, 1, 10),
            Notes = "Delivery notes"
        };
        ctx.CourseDeliveries.Add(delivery);

        var s1 = new Student { FirstName = "Alice", LastName = "Smith" };
        var s2 = new Student { FirstName = "Bob", LastName = "Jones" };
        ctx.Students.AddRange(s1, s2);

        var a1 = new Allocation
        {
            CourseDeliveryId = delivery.Id,
            StudentId = s1.Id,
            AttendanceStatus = AttendanceStatus.Attended,
            OutcomeStatus = OutcomeStatus.Completed
        };
        var a2 = new Allocation
        {
            CourseDeliveryId = delivery.Id,
            StudentId = s2.Id,
            AttendanceStatus = AttendanceStatus.Confirmed,
            OutcomeStatus = OutcomeStatus.Pending,
            OutcomeNotes = "Needs follow-up"
        };
        ctx.Allocations.AddRange(a1, a2);
        ctx.SaveChanges();
        return (delivery, a1, a2);
    }

    // ── GenerateDraftAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDraft_CreatesParticipants_WithCorrectNames()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, a2) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id, a2.Id }, "Trainer One");

        Assert.Equal(2, signOff.Participants.Count);
        Assert.Equal("Alice Smith", signOff.Participants[0].StudentDisplayName);
        Assert.Equal("Bob Jones", signOff.Participants[1].StudentDisplayName);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_SetsVersionOne_ForFirstSignOff()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        Assert.Equal(1, signOff.Version);
        Assert.Equal(SignOffStatus.Draft, signOff.Status);
        Assert.NotNull(signOff.DisplayId);
        Assert.StartsWith("SGN", signOff.DisplayId!);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_IncrementsVersion_ForSubsequentSignOffs()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var v1 = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var v2 = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_SetsParticipantAttended_BasedOnAllocation()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, a2) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id, a2.Id }, "Trainer");

        // a1 has AttendanceStatus.Attended, a2 has AttendanceStatus.Confirmed
        Assert.True(signOff.Participants[0].Attended);
        Assert.True(signOff.Participants[1].Attended);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_CopiesOutcomeNotes()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, _, a2) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a2.Id }, "Trainer");

        Assert.Equal("Needs follow-up", signOff.Participants[0].ParticipantNote);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_PopulatesDefaultSignatoryNames()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        Assert.Equal("Auth Person", signOff.AuthorisedByName);
        Assert.Equal("Manager", signOff.AuthorisedByPosition);
        Assert.Equal("Verify Person", signOff.VerifiedByName);
        Assert.Equal("Director", signOff.VerifiedByPosition);
        ctx.Dispose();
    }

    [Fact]
    public async Task GenerateDraft_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "Generated");
        Assert.NotNull(audit);
        Assert.Equal("SignOff", audit!.EntityType);
        Assert.Equal(signOff.DisplayId, audit.EntityDisplayId);
        ctx.Dispose();
    }

    // ── GetForDeliveryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetForDelivery_ReturnsSignOffsForDelivery_OrderedByVersionDesc()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        var list = await svc.GetForDeliveryAsync(delivery.Id);

        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].Version); // newest first
        Assert.Equal(1, list[1].Version);
        ctx.Dispose();
    }

    [Fact]
    public async Task GetForDelivery_IncludesParticipantsAndFileDocument()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        // Link a dummy document
        var doc = new Document { DisplayName = "Test.pdf", StoredFileName = "t.pdf", RelativePath = "t.pdf" };
        ctx.Documents.Add(doc);
        ctx.SaveChanges();
        await svc.SetFileDocumentIdAsync(signOff.Id, doc.Id);

        var list = await svc.GetForDeliveryAsync(delivery.Id);

        Assert.Single(list);
        Assert.NotEmpty(list[0].Participants);
        Assert.NotNull(list[0].FileDocument);
        Assert.Equal("Test.pdf", list[0].FileDocument!.DisplayName);
        ctx.Dispose();
    }

    [Fact]
    public async Task GetForDelivery_ReturnsEmpty_WhenNoSignOffs()
    {
        var (ctx, svc, _) = CreateServices();
        var list = await svc.GetForDeliveryAsync(Guid.NewGuid());
        Assert.Empty(list);
        ctx.Dispose();
    }

    // ── LockAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Lock_SetsStatusAndLockedDate()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(SignOffStatus.Signed, reloaded!.Status);
        Assert.NotNull(reloaded.LockedDate);
        ctx.Dispose();
    }

    [Fact]
    public async Task Lock_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "Locked");
        Assert.NotNull(audit);
        ctx.Dispose();
    }

    [Fact]
    public async Task Lock_ThrowsWhenAlreadyLocked()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LockAsync(signOff.Id));
        ctx.Dispose();
    }

    // ── UpdateSignedDatesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateSignedDates_PersistsDates()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var d1 = new DateTime(2026, 3, 1);
        var d2 = new DateTime(2026, 3, 2);
        var d3 = new DateTime(2026, 3, 3);
        await svc.UpdateSignedDatesAsync(signOff.Id, d1, d2, d3);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(d1, reloaded!.TrainerSignedDate);
        Assert.Equal(d2, reloaded.AuthorisedSignedDate);
        Assert.Equal(d3, reloaded.VerifiedSignedDate);
        ctx.Dispose();
    }

    [Fact]
    public async Task UpdateSignedDates_ThrowsWhenLocked()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateSignedDatesAsync(signOff.Id, DateTime.Now, null, null));
        ctx.Dispose();
    }

    [Fact]
    public async Task UpdateSignedDates_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.UpdateSignedDatesAsync(signOff.Id, DateTime.Now, null, null);

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "UpdatedSignedDates");
        Assert.NotNull(audit);
        ctx.Dispose();
    }

    // ── UpdateDetailsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDetails_PersistsAllFields()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.UpdateDetailsAsync(signOff.Id,
            "New Trainer", "New Details",
            "New Auth", "New Auth Pos",
            "New Verify", "New Verify Pos",
            "New Notes");

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal("New Trainer", reloaded!.TrainerName);
        Assert.Equal("New Details", reloaded.TrainerDetails);
        Assert.Equal("New Auth", reloaded.AuthorisedByName);
        Assert.Equal("New Auth Pos", reloaded.AuthorisedByPosition);
        Assert.Equal("New Verify", reloaded.VerifiedByName);
        Assert.Equal("New Verify Pos", reloaded.VerifiedByPosition);
        Assert.Equal("New Notes", reloaded.Notes);
        ctx.Dispose();
    }

    [Fact]
    public async Task UpdateDetails_ThrowsWhenLocked()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateDetailsAsync(signOff.Id, "T", "D", "A", "AP", "V", "VP", "N"));
        ctx.Dispose();
    }

    [Fact]
    public async Task UpdateDetails_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.UpdateDetailsAsync(signOff.Id, "T", "D", "A", "AP", "V", "VP", "N");

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "UpdatedDetails");
        Assert.NotNull(audit);
        ctx.Dispose();
    }

    // ── SupersedeAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Supersede_SetsStatusToSuperseded()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.SupersedeAsync(signOff.Id);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(SignOffStatus.Superseded, reloaded!.Status);
        ctx.Dispose();
    }

    [Fact]
    public async Task Supersede_ThrowsWhenLocked()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SupersedeAsync(signOff.Id));
        ctx.Dispose();
    }

    [Fact]
    public async Task Supersede_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.SupersedeAsync(signOff.Id);

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "Superseded");
        Assert.NotNull(audit);
        ctx.Dispose();
    }

    // ── SetFileDocumentIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SetFileDocumentId_PersistsAssociation()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var docId = Guid.NewGuid();
        ctx.Documents.Add(new Document { Id = docId, DisplayName = "Draft.pdf", StoredFileName = "d.pdf", RelativePath = "d.pdf" });
        ctx.SaveChanges();

        await svc.SetFileDocumentIdAsync(signOff.Id, docId);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(docId, reloaded!.FileDocumentId);
        ctx.Dispose();
    }

    [Fact]
    public async Task SetFileDocumentId_CreatesAuditWithDocumentId()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var docId = Guid.NewGuid();
        ctx.Documents.Add(new Document { Id = docId, DisplayName = "Draft.pdf", StoredFileName = "d.pdf", RelativePath = "d.pdf" });
        ctx.SaveChanges();

        await svc.SetFileDocumentIdAsync(signOff.Id, docId);

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "LinkedDocument");
        Assert.NotNull(audit);
        Assert.Contains(docId.ToString(), audit!.NewValuesJson!);
        ctx.Dispose();
    }

    [Fact]
    public async Task SetFileDocumentId_CanReplace_PreservingPriorDocument()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var draftDocId = Guid.NewGuid();
        var signedDocId = Guid.NewGuid();
        ctx.Documents.Add(new Document { Id = draftDocId, DisplayName = "Draft.pdf", StoredFileName = "d.pdf", RelativePath = "d.pdf" });
        ctx.Documents.Add(new Document { Id = signedDocId, DisplayName = "Signed.pdf", StoredFileName = "s.pdf", RelativePath = "s.pdf" });
        ctx.SaveChanges();

        await svc.SetFileDocumentIdAsync(signOff.Id, draftDocId);
        await svc.SetFileDocumentIdAsync(signOff.Id, signedDocId);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(signedDocId, reloaded!.FileDocumentId);

        // The original draft document is still in the Documents table (not deleted)
        var draftDoc = await ctx.Documents.FindAsync(draftDocId);
        Assert.NotNull(draftDoc);
        ctx.Dispose();
    }

    // ── SetStatusReadyForSignatureAsync ──────────────────────────────────────

    [Fact]
    public async Task SetStatusReadyForSignature_SetsStatus()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.SetStatusReadyForSignatureAsync(signOff.Id);

        var reloaded = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(SignOffStatus.ReadyForSignature, reloaded!.Status);
        ctx.Dispose();
    }

    [Fact]
    public async Task SetStatusReadyForSignature_ThrowsWhenLocked()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.LockAsync(signOff.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SetStatusReadyForSignatureAsync(signOff.Id));
        ctx.Dispose();
    }

    [Fact]
    public async Task SetStatusReadyForSignature_CreatesAuditEntry()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.SetStatusReadyForSignatureAsync(signOff.Id);

        var audit = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == signOff.Id && a.Action == "ReadyForSignature");
        Assert.NotNull(audit);
        ctx.Dispose();
    }

    // ── GetAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_IncludesAllNavigationProperties()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        var doc = new Document { DisplayName = "File.pdf", StoredFileName = "f.pdf", RelativePath = "f.pdf" };
        ctx.Documents.Add(doc);
        ctx.SaveChanges();
        await svc.SetFileDocumentIdAsync(signOff.Id, doc.Id);

        var loaded = await svc.GetAsync(signOff.Id);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.CourseDelivery);
        Assert.NotNull(loaded.CourseDelivery!.CourseDefinition);
        Assert.NotEmpty(loaded.Participants);
        Assert.NotNull(loaded.FileDocument);
        ctx.Dispose();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var (ctx, svc, _) = CreateServices();
        var loaded = await svc.GetAsync(Guid.NewGuid());
        Assert.Null(loaded);
        ctx.Dispose();
    }

    // ── Full lifecycle: generate → sign dates → lock ────────────────────────

    [Fact]
    public async Task FullLifecycle_GenerateUpdateLock()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, a2) = SeedDeliveryWithAllocations(ctx);

        // Generate
        var signOff = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id, a2.Id }, "Trainer");
        Assert.Equal(SignOffStatus.Draft, signOff.Status);

        // Update details
        await svc.UpdateDetailsAsync(signOff.Id, "Updated Trainer", "Updated Biz",
            "Auth Name", "Auth Pos", "Ver Name", "Ver Pos", "Some notes");

        // Set signed dates
        var trainerDate = new DateTime(2026, 5, 1);
        var authDate = new DateTime(2026, 5, 2);
        var verDate = new DateTime(2026, 5, 3);
        await svc.UpdateSignedDatesAsync(signOff.Id, trainerDate, authDate, verDate);

        // Ready for signature
        await svc.SetStatusReadyForSignatureAsync(signOff.Id);

        // Lock
        await svc.LockAsync(signOff.Id);

        var final = await ctx.SignOffs.FindAsync(signOff.Id);
        Assert.Equal(SignOffStatus.Signed, final!.Status);
        Assert.NotNull(final.LockedDate);
        Assert.Equal(trainerDate, final.TrainerSignedDate);
        Assert.Equal(authDate, final.AuthorisedSignedDate);
        Assert.Equal(verDate, final.VerifiedSignedDate);
        Assert.Equal("Updated Trainer", final.TrainerName);

        // All locked — no further changes allowed
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateDetailsAsync(signOff.Id, "X", null, null, null, null, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateSignedDatesAsync(signOff.Id, null, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SupersedeAsync(signOff.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LockAsync(signOff.Id));

        // Verify audit trail covers the full lifecycle
        var audits = await ctx.AuditLogs.Where(a => a.EntityId == signOff.Id).OrderBy(a => a.Timestamp).ToListAsync();
        var actions = audits.Select(a => a.Action).ToList();
        Assert.Contains("Generated", actions);
        Assert.Contains("UpdatedDetails", actions);
        Assert.Contains("UpdatedSignedDates", actions);
        Assert.Contains("ReadyForSignature", actions);
        Assert.Contains("Locked", actions);

        ctx.Dispose();
    }

    // ── Supersede + regenerate version flow ──────────────────────────────────

    [Fact]
    public async Task SupersedeAndRegenerate_CreatesNewVersion_OriginalPreserved()
    {
        var (ctx, svc, _) = CreateServices();
        var (delivery, a1, _) = SeedDeliveryWithAllocations(ctx);

        var v1 = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");
        await svc.SupersedeAsync(v1.Id);

        var v2 = await svc.GenerateDraftAsync(delivery.Id, new List<Guid> { a1.Id }, "Trainer");

        Assert.Equal(SignOffStatus.Superseded, (await ctx.SignOffs.FindAsync(v1.Id))!.Status);
        Assert.Equal(SignOffStatus.Draft, v2.Status);
        Assert.Equal(2, v2.Version);

        // Both sign-offs exist
        var all = await svc.GetForDeliveryAsync(delivery.Id);
        Assert.Equal(2, all.Count);
        ctx.Dispose();
    }
}
