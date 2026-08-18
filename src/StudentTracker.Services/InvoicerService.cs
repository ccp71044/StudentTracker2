using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class InvoicerService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DataLocationService _dataLocation;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public InvoicerService(StudentTrackerDbContext context, DataLocationService dataLocation, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _dataLocation = dataLocation;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<List<Allocation>> GetUnexportedBillableAsync() => await _context.Allocations
        .Where(a => a.IsBillable && a.ExportedInBatchId == null)
        .Include(a => a.Student)
        .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
        .ToListAsync();

    public async Task<ExportBatch> ExportAsync(List<Guid> allocationIds, string? notes = null)
    {
        var allocations = await _context.Allocations
            .Where(a => allocationIds.Contains(a.Id) && a.IsBillable && a.ExportedInBatchId == null)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .ToListAsync();

        var batch = new ExportBatch
        {
            DisplayId = _idGenerator.NextDisplayId<ExportBatch>("EXP"),
            ItemCount = allocations.Count,
            TotalAmount = allocations.Sum(a => a.CertificateCost ?? 0m),
            Notes = notes,
            ExportedAt = DateTime.UtcNow
        };
        _context.ExportBatches.Add(batch);
        await _context.SaveChangesAsync();

        var items = allocations.Select(a => new ExportBatchItem
        {
            ExportBatchId = batch.Id,
            AllocationId = a.Id,
            Quantity = 1,
            Rate = a.CertificateCost ?? 0m,
            Amount = a.CertificateCost ?? 0m
        }).ToList();
        _context.ExportBatchItems.AddRange(items);

        foreach (var a in allocations)
        {
            a.ExportedInBatchId = batch.Id;
            a.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        _audit.Record("Exported", "ExportBatch", batch.Id, batch.DisplayId);
        await _context.SaveChangesAsync();

        var exportDir = Path.Combine(_dataLocation.IntegrationPath, "InvoicerExport");
        Directory.CreateDirectory(exportDir);
        var fileNameBase = $"invoicer-export-{batch.DisplayId}";

        var records = allocations.Select(a => new InvoicerExportRecord
        {
            ExportBatchId = batch.DisplayId,
            AllocationId = a.DisplayId,
            StudentId = a.Student?.DisplayId,
            StudentName = a.Student?.FullName ?? "",
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode,
            CourseTitle = a.CourseDelivery?.CourseDefinition?.CourseTitle,
            CourseDeliveryId = a.CourseDelivery?.DisplayId,
            DeliveryDate = a.CourseDelivery?.StartDate?.ToString("dd/MM/yyyy"),
            CertificateOrderId = null,
            OrderedDate = null,
            DeliveredDate = null,
            BillableTrigger = _context.AppSettings.First().BillableTrigger,
            Quantity = 1,
            Rate = a.CertificateCost ?? 0m,
            Amount = a.CertificateCost ?? 0m,
            Notes = a.OutcomeNotes
        }).ToList();

        await File.WriteAllTextAsync(Path.Combine(exportDir, $"{fileNameBase}.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        await using (var writer = new StreamWriter(Path.Combine(exportDir, $"{fileNameBase}.csv"), false, Encoding.UTF8))
        await using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 }))
        {
            await csv.WriteRecordsAsync(records);
        }

        return batch;
    }
}

public class InvoicerExportRecord
{
    public string? ExportBatchId { get; set; }
    public string? AllocationId { get; set; }
    public string? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? CourseCode { get; set; }
    public string? CourseTitle { get; set; }
    public string? CourseDeliveryId { get; set; }
    public string? DeliveryDate { get; set; }
    public string? CertificateOrderId { get; set; }
    public string? OrderedDate { get; set; }
    public string? DeliveredDate { get; set; }
    public string? BillableTrigger { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
