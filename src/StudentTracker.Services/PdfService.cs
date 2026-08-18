using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class PdfService
{
    private readonly StudentTrackerDbContext _context;

    public PdfService(StudentTrackerDbContext context)
    {
        _context = context;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateSignOffPdf(Guid signOffId, string? logoPath = null)
    {
        var signOff = _context.SignOffs
            .Include(s => s.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(s => s.Participants)
            .First(s => s.Id == signOffId);

        var delivery = signOff.CourseDelivery!;
        var course = delivery.CourseDefinition!;
        var participants = signOff.Participants.OrderBy(p => p.SortOrder).ToList();

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Course Delivery Completion Sign-Off").Bold().FontSize(18);
                        col.Item().Text($"Document ID: {signOff.DisplayId}  |  Version: {signOff.Version}  |  Generated: {signOff.GeneratedDate:dd/MM/yyyy HH:mm}");
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("Course delivered: ").Bold();
                        text.Span($"{course.CourseCode} - {course.CourseTitle}");
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Trainer: ").Bold();
                        text.Span(signOff.TrainerName ?? delivery.TrainerName ?? "Not specified");
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Trainer business/provider details: ").Bold();
                        text.Span(signOff.TrainerDetails ?? delivery.TrainerBusinessDetails ?? string.Empty);
                    });

                    col.Item().PaddingTop(10).Text("Participants:").Bold();

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Date delivered").Bold();
                            header.Cell().Text("Participant name").Bold();
                            header.Cell().Text("Attended").Bold();
                            header.Cell().Text("Participant note").Bold();
                        });

                        foreach (var p in participants)
                        {
                            table.Cell().Text(p.DeliveryDateText ?? "");
                            table.Cell().Text(p.StudentDisplayName ?? "");
                            table.Cell().Text(p.Attended ? "Yes" : "No");
                            table.Cell().Text(p.ParticipantNote ?? "");
                        }
                    });

                    col.Item().PaddingTop(20).Text("Declaration").Bold();
                    col.Item().Text("This document serves as a record of training delivery and participant attendance. It confirms that the course listed above was delivered by a suitably qualified trainer on the dates specified, and that the participants named attended the training session(s). The trainer and authorised representative acknowledge that the information contained within this record is accurate and has been completed in accordance with organisational training and record-keeping requirements.");

                    col.Item().PaddingTop(20).Text("Trainer Declaration").Bold();
                    col.Item().Text($"Name: {signOff.TrainerName ?? delivery.TrainerName ?? ""}");
                    col.Item().Text($"Signature: ____________________________    Date: {signOff.TrainerSignedDate:dd/MM/yyyy}");

                    col.Item().PaddingTop(16).Text("Authorised By (for SCJV)").Bold();
                    col.Item().Text($"Name: {signOff.AuthorisedByName ?? ""}");
                    col.Item().Text($"Position: {signOff.AuthorisedByPosition ?? ""}");
                    col.Item().Text($"Signature: ____________________________    Date: {signOff.AuthorisedSignedDate:dd/MM/yyyy}");

                    col.Item().PaddingTop(16).Text("Verified By (Town and Country)").Bold();
                    col.Item().Text($"Name: {signOff.VerifiedByName ?? ""}");
                    col.Item().Text($"Position: {signOff.VerifiedByPosition ?? ""}");
                    col.Item().Text($"Signature: ____________________________    Date: {signOff.VerifiedSignedDate:dd/MM/yyyy}");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
