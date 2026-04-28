---
name: questpdf-certs
description: >
  QuestPDF certificate generation patterns for CertificateService.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Setup

- License: `QuestPDF.Settings.License = LicenseType.Community;` set
  once at service startup (already in CertificateService Program.cs)
- Templates live in `src/services/LMS.CertificateService/Templates/`
- Fonts embedded via `IFontProvider`, not loaded from disk at runtime

## Document pattern

```csharp
public sealed class CompletionCertificate : IDocument
{
    private readonly CertificateData _data;
    public CompletionCertificate(CertificateData data) => _data = data;

    public DocumentMetadata GetMetadata() => new() {
        Title = $"Certificate — {_data.CourseName}",
        Author = "LMS",
    };

    public void Compose(IDocumentContainer c) =>
        c.Page(p => {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(1, Unit.Centimetre);
            p.Header().Element(Header);
            p.Content().Element(Body);
            p.Footer().Element(Footer);
        });
    // ...
}
```

## Rules

- Never block: render in a background queue (consumer of
  `CourseCompleted`)
- Output to object storage with key `t/{tenantId}/cert/{userId}/{courseId}.pdf`
- Sign URL on download; default 15-min expiry
- Locale + RTL: pull from user profile; QuestPDF supports both via
  `ContentFromRightToLeft()`
- Tests: render to byte array, assert `%PDF-` prefix and page count
