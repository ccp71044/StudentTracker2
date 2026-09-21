using System.Globalization;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Imports invoice reference data from the Invoicer application. Matching is by external invoice
/// ID first, then invoice number. Existing records are updated rather than duplicated.
/// </summary>
public class InvoicerReferenceImportService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly IDocumentService _documentService;
    private readonly AuditService _audit;

    public InvoicerReferenceImportService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, IDocumentService documentService, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _documentService = documentService;
        _audit = audit;
    }

    public async Task<List<Invoice>> GetLatestInvoicesAsync(int count = 50) =>
        await _context.Invoices
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<InvoicerReferenceImportResult> ImportFromFileAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return await ImportAsync(stream);
    }

    public async Task<InvoicerReferenceImportResult> ImportAsync(Stream stream)
    {
        var result = new InvoicerReferenceImportResult();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            Encoding = Encoding.UTF8
        });

        var records = new List<InvoiceReferenceCsvRecord>();
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            var record = csv.GetRecord<InvoiceReferenceCsvRecord>();
            if (record != null)
                records.Add(record);
        }

        foreach (var record in records)
        {
            try
            {
                var (invoice, isNew) = await FindOrCreateInvoiceAsync(record);
                var changed = ApplyRecord(invoice, record);

                if (invoice.DisplayId == null)
                    invoice.DisplayId = _idGenerator.NextDisplayId<Invoice>("INV");

                if (!string.IsNullOrWhiteSpace(record.PdfPath) && File.Exists(record.PdfPath) && invoice.FileDocumentId == null)
                {
                    var document = await _documentService.AddDocumentAsync(
                        record.PdfPath,
                        "InvoicerImport",
                        displayName: Path.GetFileName(record.PdfPath),
                        description: $"Invoice {record.InvoiceNumber} imported from Invoicer",
                        receivedDate: record.InvoiceDate);
                    invoice.FileDocumentId = document.Id;
                    await _documentService.LinkDocumentAsync(document.Id, nameof(Invoice), invoice.Id, "InvoiceFile");
                    changed = true;
                }

                if (isNew)
                {
                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();
                    _audit.Record("Imported", nameof(Invoice), invoice.Id, invoice.DisplayId);
                    await _context.SaveChangesAsync();
                    result.ImportedCount++;
                }
                else if (changed)
                {
                    _context.Invoices.Update(invoice);
                    await _context.SaveChangesAsync();
                    _audit.Record("Updated", nameof(Invoice), invoice.Id, invoice.DisplayId);
                    await _context.SaveChangesAsync();
                    result.UpdatedCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Record {record.InvoiceNumber ?? record.ExternalInvoiceId ?? $"row {result.Total + 1}"}: {ex.Message}");
            }

            result.Total++;
        }

        return result;
    }

    private async Task<(Invoice Invoice, bool IsNew)> FindOrCreateInvoiceAsync(InvoiceReferenceCsvRecord record)
    {
        Invoice? invoice = null;

        if (!string.IsNullOrWhiteSpace(record.ExternalInvoiceId))
            invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.ExternalInvoiceId == record.ExternalInvoiceId);

        if (invoice == null && !string.IsNullOrWhiteSpace(record.InvoiceNumber))
            invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == record.InvoiceNumber);

        if (invoice == null)
        {
            invoice = new Invoice { Id = Guid.NewGuid() };
            return (invoice, true);
        }

        return (invoice, false);
    }

    private static bool ApplyRecord(Invoice invoice, InvoiceReferenceCsvRecord record)
    {
        var changed = false;

        if (SetIfDifferent(invoice.ExternalInvoiceId, record.ExternalInvoiceId, v => invoice.ExternalInvoiceId = v)) changed = true;
        if (SetIfDifferent(invoice.InvoiceNumber, record.InvoiceNumber, v => invoice.InvoiceNumber = v)) changed = true;
        if (SetIfDifferent(invoice.Customer, record.Customer, v => invoice.Customer = v)) changed = true;
        if (invoice.InvoiceDate != record.InvoiceDate) { invoice.InvoiceDate = record.InvoiceDate; changed = true; }
        if (invoice.DueDate != record.DueDate) { invoice.DueDate = record.DueDate; changed = true; }
        if (invoice.TotalAmount != record.TotalAmount) { invoice.TotalAmount = record.TotalAmount; changed = true; }
        if (invoice.GSTAmount != record.GSTAmount) { invoice.GSTAmount = record.GSTAmount; changed = true; }
        if (SetIfDifferent(invoice.PaymentStatus, record.PaymentStatus, v => invoice.PaymentStatus = v)) changed = true;
        if (invoice.AmountAssignedToStudentTracker != record.AmountAssignedToStudentTracker) { invoice.AmountAssignedToStudentTracker = record.AmountAssignedToStudentTracker; changed = true; }
        if (SetIfDifferent(invoice.Notes, record.Notes, v => invoice.Notes = v)) changed = true;

        return changed;
    }

    private static bool SetIfDifferent(string? current, string? value, Action<string?> setter)
    {
        if (current != value)
        {
            setter(value);
            return true;
        }
        return false;
    }
}

public class InvoiceReferenceCsvRecord
{
    public string? ExternalInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Customer { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? GSTAmount { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? AmountAssignedToStudentTracker { get; set; }
    public string? PdfPath { get; set; }
    public string? Notes { get; set; }
}

public class InvoicerReferenceImportResult
{
    public int Total { get; set; }
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; } = new();
    public List<Guid> Imported { get; } = new();
}
