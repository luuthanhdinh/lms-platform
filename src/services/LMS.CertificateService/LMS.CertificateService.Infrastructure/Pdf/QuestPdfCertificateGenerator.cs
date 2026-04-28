using LMS.CertificateService.Domain.Entities;
using LMS.CertificateService.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace LMS.CertificateService.Infrastructure.Pdf;

public sealed class QuestPdfCertificateGenerator : ICertificatePdfGenerator
{
    static QuestPdfCertificateGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateAsync(Certificate cert, CancellationToken ct = default)
    {
        var learnerName = cert.LearnerName ?? "Student";
        var courseName = cert.CourseName ?? "Course";
        var qrBytes = GenerateQrCode(cert.VerificationCode.ToString("N").ToUpperInvariant());

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(ts => ts.FontSize(12));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text("Certificate of Completion")
                       .FontSize(32).Bold();

                    col.Item().Height(20);

                    col.Item().AlignCenter().Text("This certifies that")
                       .FontSize(16);

                    col.Item().Height(10);

                    col.Item().AlignCenter().Text(learnerName)
                       .FontSize(24).Bold();

                    col.Item().Height(10);

                    col.Item().AlignCenter().Text("has successfully completed")
                       .FontSize(16);

                    col.Item().Height(10);

                    col.Item().AlignCenter().Text(courseName)
                       .FontSize(20).Bold();

                    col.Item().Height(30);

                    col.Item().AlignCenter().Text($"Issued on: {cert.IssuedAt:MMMM dd, yyyy}")
                       .FontSize(14);

                    col.Item().Height(10);

                    col.Item().AlignCenter().Text($"Certificate No: {cert.CertificateNumber}")
                       .FontSize(12);

                    col.Item().Height(20);

                    // QR code
                    col.Item().AlignCenter().Width(80).Image(qrBytes);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private static byte[] GenerateQrCode(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(3);
    }
}
