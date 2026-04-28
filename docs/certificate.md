# CertificateService

**Port:** 5107 | **DB:** `lms_certificate` (PostgreSQL) + S3 | **ADRs:** ADR-008

## Responsibility
Generates PDF certificates on CourseCompleted events.
Public verification endpoint — no auth required.
Phase 4 extends to Open Badges 3.0 + optional Polygon blockchain anchoring.

## Endpoints

```
GET /api/certificates/me                  → CertificateSummary[]
GET /api/certificates/{id}/download       → { downloadUrl, expiresAt }
# Phase 4 additions:
GET /api/certificates/{id}/share          → { shareCardUrl, linkedInShareUrl }
GET /api/certificates/wallet              → WalletItem[]
POST /api/certificates/{id}/revoke        ← { reason }    # admin only
# Public — no auth:
GET /verify/{verificationCode}            → HTML or JSON
GET /credentials/status/{listId}          → Status List 2021 (Phase 4)
GET /.well-known/jwks.json                → JWK Set (Phase 4)
```

## Key flow — PDF generation (ADR-008)

Triggered by `CourseCompleted` event:
1. Fetch student name from IdentityService (HTTP)
2. Fetch course title from CourseService (HTTP)
3. Generate PDF with QuestPDF:
   - Tenant logo
   - Student name (large)
   - Course title
   - Completion date
   - QR code linking to `/verify/{verificationCode}`
4. Upload PDF to S3: `certificates/{tenantId}/{userId}/{certId}.pdf`
5. Save `Certificate` record
6. Publish `CredentialIssued`

**QuestPDF template:**
```csharp
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Content().Column(col =>
        {
            col.Item().Text(model.TenantName).FontSize(14);
            col.Item().Text("Certificate of Completion").FontSize(28).Bold();
            col.Item().Text($"This certifies that {model.StudentName}").FontSize(16);
            col.Item().Text($"has completed {model.CourseTitle}").FontSize(20).Bold();
            col.Item().Text(model.CompletedAt.ToString("MMMM d, yyyy"));
            col.Item().Element(e => e.QrCode($"https://lms.platform/verify/{model.VerificationCode}"));
        });
    });
});
```

## Events published
- `CredentialIssued`

## Events consumed
- `CourseCompleted` → generate certificate

## Entities
See `docs/entities.md` → CertificateService section.
