using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class CertificateService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly CreditService _creditService;
    private readonly AuditService _audit;

    public CertificateService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, CreditService creditService, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _creditService = creditService;
        _audit = audit;
    }

    public async Task<List<CertificateOrder>> GetOrdersAsync()
    {
        return await _context.CertificateOrders
            .Include(o => o.Allocation).ThenInclude(a => a!.Student)
            .Include(o => o.Allocation).ThenInclude(a => a!.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .OrderByDescending(o => o.OrderedDate)
            .ToListAsync();
    }

    public async Task<List<CertificateDelivery>> GetDeliveriesAsync(Guid certificateOrderId)
    {
        return await _context.CertificateDeliveries
            .Where(d => d.CertificateOrderId == certificateOrderId)
            .Include(d => d.EvidenceDocument)
            .OrderByDescending(d => d.DeliveredDate)
            .ToListAsync();
    }

    public async Task<CertificateOrder> OrderCertificateAsync(Guid allocationId, string provider, string? externalReference = null, string? notes = null, bool isReplacement = false, string? replacementReason = null, bool overrideEligibility = false)
    {
        var allocation = await _context.Allocations
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .FirstOrDefaultAsync(a => a.Id == allocationId) ?? throw new ArgumentException("Allocation not found");

        var existing = await _context.CertificateOrders.AnyAsync(o => o.AllocationId == allocationId && o.Status == CertificateOrderStatus.Ordered && !o.IsReplacement);
        if (existing && !isReplacement) throw new InvalidOperationException("A normal certificate order already exists for this allocation. Use replacement flow if needed.");

        if (allocation.OutcomeStatus != OutcomeStatus.Completed && !overrideEligibility)
            throw new InvalidOperationException("Certificate can only be ordered for completed allocations.");

        if (allocation.CreditStatus != CreditStatus.Allocated && !overrideEligibility)
            throw new InvalidOperationException("Credit must be allocated before ordering a certificate.");

        if (allocation.CreditPoolId == null) throw new InvalidOperationException("No credit pool linked to allocation.");

        var cost = allocation.CertificateCost ?? allocation.CourseDelivery?.CourseDefinition?.DefaultCertificateCost ?? 0m;
        var order = new CertificateOrder
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateOrder>("ORD"),
            AllocationId = allocationId,
            OrderedDate = DateTime.UtcNow,
            Provider = provider,
            ExternalReference = externalReference,
            Quantity = 1,
            Notes = notes,
            Status = CertificateOrderStatus.Ordered,
            IsReplacement = isReplacement,
            ReplacementReason = replacementReason
        };
        _context.CertificateOrders.Add(order);
        await _context.SaveChangesAsync();

        await _creditService.ConsumeAsync(allocation.CreditPoolId.Value, allocationId, Math.Abs(cost), 1, notes, CreditTransactionType.OrderConsume);
        allocation.CertificateOrderStatus = CertificateOrderStatus.Ordered;
        allocation.CertificateDeliveryStatus = CertificateDeliveryStatus.Awaiting;
        allocation.UpdatedAt = DateTime.UtcNow;

        order.CreditTransactionId = (await _context.CertificateCreditTransactions
            .Where(t => t.AllocationId == allocationId && t.TransactionType == CreditTransactionType.OrderConsume)
            .OrderByDescending(t => t.TransactionDateTime)
            .FirstOrDefaultAsync())?.Id;

        await _context.SaveChangesAsync();
        _audit.Record("Ordered", "CertificateOrder", order.Id, order.DisplayId);

        await UpdateBillableAsync(allocation);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<CertificateDelivery> RecordDeliveryAsync(Guid certificateOrderId, DateTime deliveredDate, string method, string deliveredTo, string? notes = null, Guid? evidenceDocumentId = null, string? recipientDetails = null)
    {
        var order = await _context.CertificateOrders
            .Include(o => o.Allocation)
            .FirstOrDefaultAsync(o => o.Id == certificateOrderId) ?? throw new ArgumentException("Certificate order not found");
        Document? evidenceDocument = null;
        if (evidenceDocumentId.HasValue)
        {
            evidenceDocument = await _context.Documents.FindAsync(evidenceDocumentId.Value)
                ?? throw new ArgumentException("Evidence document not found", nameof(evidenceDocumentId));
            if (evidenceDocument.Status != DocumentStatus.Active)
                throw new InvalidOperationException("Certificate evidence must be an active document.");
        }

        var delivery = new CertificateDelivery
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateDelivery>("CDV"),
            CertificateOrderId = certificateOrderId,
            DeliveredDate = deliveredDate,
            DeliveryMethod = method,
            DeliveredTo = deliveredTo,
            RecipientDetails = recipientDetails,
            EvidenceDocumentId = evidenceDocumentId,
            Notes = notes
        };
        _context.CertificateDeliveries.Add(delivery);

        var allocation = order.Allocation ?? throw new ArgumentException("Allocation not found");
        allocation.CertificateDeliveryStatus = CertificateDeliveryStatus.Delivered;
        allocation.UpdatedAt = DateTime.UtcNow;

        if (evidenceDocument != null)
        {
            var targets = new List<(string EntityType, Guid EntityId)>
            {
                ("CertificateDelivery", delivery.Id),
                ("CertificateOrder", order.Id),
                ("Allocation", allocation.Id)
            };
            if (allocation.StudentId.HasValue)
                targets.Add(("Student", allocation.StudentId.Value));
            foreach (var target in targets.Distinct())
            {
                var link = new DocumentLink
                {
                    DocumentId = evidenceDocument.Id,
                    EntityType = target.EntityType,
                    EntityId = target.EntityId,
                    LinkPurpose = "Issued certificate evidence"
                };
                _context.DocumentLinks.Add(link);
                _audit.Record("Linked", "DocumentLink", link.Id, null, null,
                    new { evidenceDocumentId = evidenceDocument.Id, target.EntityType, target.EntityId, CertificateDeliveryId = delivery.Id });
            }
        }

        await _context.SaveChangesAsync();
        _audit.Record("Delivered", "CertificateDelivery", delivery.Id, delivery.DisplayId, null,
            new { delivery.EvidenceDocumentId, order.AllocationId, allocation.StudentId });

        await UpdateBillableAsync(allocation);
        await _context.SaveChangesAsync();
        return delivery;
    }

    public async Task UpdateBillableAsync(Allocation allocation)
    {
        var settings = await _context.AppSettings.FirstAsync();
        var trigger = settings.BillableTrigger;
        bool shouldBeBillable = trigger switch
        {
            "Ordered" => allocation.CertificateOrderStatus == CertificateOrderStatus.Ordered,
            "Delivered" => allocation.CertificateDeliveryStatus == CertificateDeliveryStatus.Delivered,
            "Manual" => false,
            _ => false
        };
        if (shouldBeBillable && !allocation.IsBillable)
        {
            allocation.IsBillable = true;
            allocation.BillableDate = DateTime.UtcNow;
            allocation.UpdatedAt = DateTime.UtcNow;
            _audit.Record("BillableCreated", "Allocation", allocation.Id, allocation.DisplayId);
        }
    }

    public async Task<List<Allocation>> GetBillableUnexportedAsync()
    {
        return await _context.Allocations
            .Where(a => a.IsBillable && a.ExportedInBatchId == null)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .ToListAsync();
    }
}
