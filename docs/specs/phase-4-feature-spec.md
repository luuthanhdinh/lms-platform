# LMS Platform — Phase 4 Feature Specification

**Phase:** 4 — Innovation  
**Sprints:** 24–28 (10 weeks)  
**Goal:** Deliver market-differentiating features that establish the platform as a next-generation LMS. These are high-complexity, high-differentiation capabilities that build on the stable foundation of Phases 1–3.  
**Prerequisite:** All Phase 1, 2, and 3 work packages are complete and stable in production.  
**Stack:** Adds — Kubernetes Jobs API · code-server (VS Code in browser) · ttyd (browser terminal) · Polygon SDK (optional) · ethers.js · SignalR (extension) · OpenAI Whisper translate · ML.NET (extension) · Web Vitals API

---

## Table of Contents

1. [WP 10.2 — Virtual Lab Service](#wp-102--virtual-lab-service)
2. [WP 10.3 — Digital Credentials & Blockchain](#wp-103--digital-credentials--blockchain)
3. [WP 4.4 — Engagement & Wellbeing AI](#wp-44--engagement--wellbeing-ai)
4. [WP 10.5 — AI Translation Service](#wp-105--ai-translation-service)
5. [Cross-cutting Phase 4 Concerns](#cross-cutting-phase-4-concerns)
6. [Open Design Decisions](#open-design-decisions)
7. [Phase 4 Event Contract Summary](#phase-4-event-contract-summary)
8. [Phase 4 API Surface Summary](#phase-4-api-surface-summary)
9. [Full Platform — Consolidated Open Decisions](#full-platform--consolidated-open-decisions)
10. [Full Platform — Complete Event Catalog](#full-platform--complete-event-catalog)
11. [Full Platform — Complete Service Inventory](#full-platform--complete-service-inventory)

---

## WP 10.2 — Virtual Lab Service

**Sprint:** 24–28 | **Owner:** DevOps + Backend | **Effort:** 3w  
**Service:** `LMS.VirtualLabService`

### Context

Virtual labs give students a real, hands-on practice environment — a browser-based terminal or VS Code IDE — provisioned on-demand as an ephemeral Kubernetes Job. Each lab is tied to a lesson and has a pre-configured container image. Automated test scripts validate the student's work and mark the lab as complete. Labs auto-teardown after a configurable TTL to control cost.

This is the most infrastructure-intensive feature in the platform. It requires careful resource governance (namespaces, quotas, network policies) and a reliable lifecycle management loop.

### User stories

**US-10.2.1** — As a student, I can launch a hands-on lab directly from a lesson so that I practice in a real environment without installing anything.

**US-10.2.2** — As a student, my lab environment is ready within 60 seconds of clicking "Launch Lab" so that I am not kept waiting.

**US-10.2.3** — As a student, I can use a browser-based terminal or VS Code editor inside my lab so that the experience feels like a real development environment.

**US-10.2.4** — As a student, my lab validates my work automatically and marks the lab as complete when I pass so that I get immediate feedback.

**US-10.2.5** — As a student, if I close my browser and return within the TTL window, my lab session is resumed from its current state so that I don't lose progress.

**US-10.2.6** — As an instructor, I can define a lab exercise with a Docker image, startup script, validation tests, and TTL so that the lab environment is fully configured before students use it.

**US-10.2.7** — As a platform admin, I can see resource usage (CPU, memory, active pods) across all lab sessions so that I can manage costs and capacity.

### Acceptance criteria

**Lab definition (instructor):**

- [ ] `POST /api/labs/definitions` — instructor creates lab: `{ lessonId, title, description, dockerImage, startupScript?, interfaceType(Terminal|VSCode|Both), ttlMinutes(default 60, max 240), validationScript, validationTimeout(default 30s), resourceLimits{ cpu:"500m", memory:"512Mi" } }`
- [ ] `GET /api/labs/definitions?courseId={id}` — returns all lab definitions for a course
- [ ] `PUT /api/labs/definitions/{id}` — update lab (only if no active sessions)
- [ ] `DELETE /api/labs/definitions/{id}` — soft delete (cannot delete if active sessions exist)
- [ ] Instructor can upload a `validation.sh` or `validation.py` script; stored in S3 alongside lab definition
- [ ] Docker image must be pre-approved or pulled from a whitelist registry (configurable per tenant); prevents arbitrary image execution
- [ ] Lab definition includes `hints[]` — optional step-by-step hints student can reveal one at a time during the lab

**Session lifecycle:**

- [ ] `POST /api/labs/sessions` — student starts session: `{ labDefinitionId }`; validates enrollment; checks no active session already exists for this student+lab; returns `{ sessionId, status: "Provisioning" }`
- [ ] Provisioning flow (target: < 60s end-to-end):
  1. Create Kubernetes Namespace: `lab-{sessionId}` (isolated per session)
  2. Apply ResourceQuota: CPU limit, memory limit, no external egress (NetworkPolicy)
  3. Apply NetworkPolicy: deny all ingress/egress except lab-gateway → pod
  4. Create Kubernetes Job with: student's Docker image, startup script as ConfigMap, environment vars `{ USER_ID, SESSION_ID, LAB_ID }`
  5. Wait for Pod `Running`; get Pod IP
  6. Register Pod IP in lab-gateway routing table (YARP route or Envoy)
  7. Update session status → `Ready`; publish `LabSessionReady` event
- [ ] `GET /api/labs/sessions/{id}` — returns `{ status, accessUrl, remainingMinutes, validationStatus, hintsRevealed }`
- [ ] `accessUrl` = authenticated proxy URL through lab gateway: `/labs/proxy/{sessionId}/` (all traffic tunnelled; auth via JWT cookie)
- [ ] `POST /api/labs/sessions/{id}/validate` — student triggers validation; VirtualLabService executes `validationScript` inside running Pod via Kubernetes exec API; streams output; returns `{ passed: bool, output, score? }`
- [ ] On validation pass: publish `LabCompleted` event → ProgressService marks lesson complete; GamificationService awards XP
- [ ] `POST /api/labs/sessions/{id}/extend` — student requests TTL extension (max 1 extension per session, +30 minutes)
- [ ] `POST /api/labs/sessions/{id}/terminate` — student explicitly ends session; triggers teardown
- [ ] TTL enforcement: `LabTtlWorker` runs every minute; terminates sessions where `startedAt + ttlMinutes < now`; sends 5-minute warning notification before teardown
- [ ] Teardown: delete Kubernetes Job → Pod terminates → delete Namespace → update session status → `Terminated`

**Resume:**

- [ ] On student reconnect (within TTL): `GET /api/labs/sessions/active?labDefinitionId={id}` — returns active session if exists; client reconnects to same `accessUrl`
- [ ] Pod filesystem state is preserved as long as Pod is running; no persistent volume required in Phase 4 (stateless labs only)

**Browser interface:**

- [ ] Terminal interface: `ttyd` deployed as sidecar in lab Pod; exposed via proxy at `/labs/proxy/{sessionId}/terminal`; authenticated WebSocket
- [ ] VS Code interface: `code-server` deployed as sidecar; exposed at `/labs/proxy/{sessionId}/vscode`; pre-configured with workspace folder = `/workspace`
- [ ] Interface choice determined by `interfaceType` in lab definition; both can be available simultaneously
- [ ] Student sees a split-pane UI: left = instructions + hints panel, right = terminal/VS Code iframe

**Resource governance:**

- [ ] Kubernetes Namespace per session with ResourceQuota: `cpu: 500m–2000m`, `memory: 512Mi–2Gi` (configurable per lab definition)
- [ ] NetworkPolicy: deny all egress except DNS; deny all ingress except from lab-gateway namespace
- [ ] Node pool dedicated to lab workloads: separate from application workloads (node selector `workload=labs`)
- [ ] `GET /api/labs/admin/resources` — platform admin sees: `{ activeSessions, totalCpu, totalMemory, costEstimate, sessionsPerCourse[] }`
- [ ] Cost alert: if `activeSessions > configurable threshold` → alert platform admin
- [ ] Image pull policy: `IfNotPresent`; pre-pull common images on lab node pool via DaemonSet to reduce cold start time

**Admin & instructor visibility:**

- [ ] `GET /api/labs/definitions/{id}/analytics` — instructor sees: `{ totalSessions, completionRate, avgTimeToComplete, avgValidationAttempts, commonFailurePoints }`
- [ ] `GET /api/labs/admin/sessions` — platform admin; paginated active session list with resource usage per session

### Key data model

```
LabDefinition {
  Id, LessonId, CourseId, TenantId, InstructorId,
  Title, Description, DockerImage, StartupScript?,
  InterfaceType(Terminal|VSCode|Both),
  TtlMinutes, ValidationScriptS3Key, ValidationTimeout,
  ResourceLimits{ Cpu, Memory },
  Hints[]{Order, Content},
  IsActive, CreatedAt
}

LabSession {
  Id, LabDefinitionId, UserId, TenantId,
  Status(Provisioning|Ready|Validated|Terminated|Failed),
  NamespaceName, PodName, AccessUrl,
  StartedAt, ExpiresAt, TerminatedAt,
  ValidationAttempts, ValidationPassed,
  HintsRevealed, ExtensionUsed
}

LabValidationRun {
  Id, SessionId, UserId, TenantId,
  Passed, Output, Score?, DurationMs, TriggeredAt
}
```

### Open decisions for this WP

- [ ] **OD-10.2.a:** Lab gateway: YARP reverse proxy vs Envoy sidecar vs dedicated Nginx per pod? → Recommendation: YARP-based lab gateway running in its own pod; simplest .NET-native option.
- [ ] **OD-10.2.b:** Persistent storage: ephemeral (in-Pod) or PVC per session? → Phase 4 = ephemeral (stateless labs); PVC option deferred to Phase 5 (stateful multi-session labs).
- [ ] **OD-10.2.c:** Docker image registry: shared platform registry or per-tenant private registry? → Recommendation: shared platform registry with per-tenant namespace prefix; private registry as enterprise add-on.

---

## WP 10.3 — Digital Credentials & Blockchain

**Sprint:** 24–26 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.CertificateService` (major extension)

### Context

Phase 1 issued basic PDF certificates. Phase 4 upgrades to **Open Badges 3.0** — a W3C-compatible verifiable credential standard — and adds optional on-chain anchoring for tamper-proof verification. A public verification endpoint requires no login. LinkedIn auto-share is triggered on issuance.

The blockchain component is **opt-in per tenant** — it adds cost and complexity that not every customer needs. The Open Badges 3.0 credential is valuable on its own without blockchain.

### User stories

**US-10.3.1** — As a student, when I complete a course I receive a digitally signed Open Badges 3.0 credential so that it is verifiable by any Open Badges-compatible platform.

**US-10.3.2** — As a student, I can share my credential to LinkedIn with one click so that my achievement is visible to my professional network.

**US-10.3.3** — As a third party (employer, university), I can verify a credential at a public URL without logging in so that checking authenticity is frictionless.

**US-10.3.4** — As a tenant admin, I can optionally enable blockchain anchoring so that credentials are anchored to an immutable public ledger for maximum tamper-evidence.

**US-10.3.5** — As an admin, I can revoke a credential (e.g., if a student is found to have cheated) so that the verification endpoint reflects the revocation.

**US-10.3.6** — As a student, I can see all my earned credentials in a digital wallet within the platform so that I manage them from one place.

### Acceptance criteria

**Open Badges 3.0 credential issuance:**

- [ ] Consumes `CourseCompleted` event → generates Open Badges 3.0 credential (replaces Phase 1 PDF-only flow; PDF still generated alongside)
- [ ] Credential format: W3C Verifiable Credential (VC) as compact JWT (JWS); signed with platform Ed25519 private key (stored in Azure Key Vault)
- [ ] Credential payload (Open Badges 3.0 schema):
  ```json
  {
    "@context": ["https://www.w3.org/2018/credentials/v1", "https://purl.imsglobal.org/spec/ob/v3p0/context.json"],
    "type": ["VerifiableCredential", "OpenBadgeCredential"],
    "id": "https://lms.platform/credentials/{credentialId}",
    "issuer": { "id": "https://lms.platform/issuers/{tenantId}", "type": "Profile", "name": "{tenantName}" },
    "issuanceDate": "ISO8601",
    "credentialSubject": {
      "id": "did:email:{studentEmail}",
      "type": "AchievementSubject",
      "achievement": {
        "id": "https://lms.platform/achievements/{courseId}",
        "type": "Achievement",
        "name": "{courseTitle}",
        "description": "{courseDescription}",
        "criteria": { "narrative": "Completed all required lessons and assessments" },
        "image": { "id": "{badgeImageUrl}", "type": "Image" }
      }
    }
  }
  ```
- [ ] JWT signed with `Ed25519` using platform key; public key published at `GET /.well-known/jwks.json`
- [ ] Credential stored in DB as: raw JWT string + parsed fields for querying
- [ ] PDF certificate still generated alongside; both linked to same `Certificate` record

**Verification:**

- [ ] `GET /credentials/{credentialId}` — **public, no auth** — returns:
  - HTML page: student name, course, issuer, date, validity status, badge image, "Verify" check mark
  - JSON-LD (via `Accept: application/json`): full VC JSON for machine verification
  - QR code linking to this URL embeddable in PDF certificate
- [ ] Verification checks: JWT signature valid, `exp` not exceeded (credentials do not expire by default, but can be set), revocation registry check
- [ ] `GET /.well-known/jwks.json` — public; returns platform Ed25519 JWK set for independent JWT verification

**Revocation:**

- [ ] `POST /api/certificates/{id}/revoke` — admin only; `{ reason }`; adds to revocation registry
- [ ] `RevocationRegistry` stored as append-only list; checked on every verify request
- [ ] On revocation: verification page shows "This credential has been revoked" with revocation date; student notified
- [ ] Revocation uses **Status List 2021** (W3C standard) embedded in the VC at issuance time; revocation list published at `GET /credentials/status/{listId}`

**LinkedIn auto-share:**

- [ ] On credential issuance: display one-click "Add to LinkedIn" button in student notification and wallet
- [ ] LinkedIn Share URL: `https://www.linkedin.com/profile/add?startTask=CERTIFICATION_NAME&name={courseTitle}&organizationId={tenantLinkedInOrgId}&issueYear={year}&issueMonth={month}&certUrl={verifyUrl}&certId={credentialId}`
- [ ] `tenantLinkedInOrgId` configurable per tenant in TenantService

**Blockchain anchoring (opt-in):**

- [ ] Tenant admin enables via `PUT /api/tenants/{id}/blockchain` `{ enabled: true, network: "polygon-mainnet"|"polygon-amoy" }`
- [ ] On credential issuance (if enabled): compute `SHA-256(credentialJwt)` → submit as `OP_RETURN` data in Polygon transaction via `ethers.js` + Alchemy/Infura RPC
- [ ] `AnchoringWorker`: async consumer of `CredentialIssued` event; handles blockchain write; retries on RPC failure (up to 5x with exponential backoff)
- [ ] On anchor confirmed: store `{ txHash, blockNumber, network, anchoredAt }` in `Certificate.BlockchainAnchor`
- [ ] Verification page shows: "Anchored on Polygon" badge with link to `polygonscan.com/tx/{txHash}` when anchor present
- [ ] If anchoring fails after retries: credential still valid and issued; anchor failure logged and alerted (not blocking)

**Student wallet:**

- [ ] `GET /api/certificates/wallet` — returns all student's credentials with: credentialId, courseTitle, issuedAt, verifyUrl, linkedInShareUrl, anchorStatus, pdfDownloadUrl
- [ ] `GET /api/certificates/{id}/share` — returns shareable card image (OG image) for social sharing: 1200×630px PNG with badge, student name, course title, issuer

### Key data model

```
Certificate {
  Id, UserId, CourseId, TenantId,
  CredentialJwt, CredentialId(UUID),
  PdfS3Key, BadgeImageUrl,
  IssuedAt, RevokedAt?, RevocationReason?,
  StatusListIndex,
  BlockchainAnchor {
    TxHash?, BlockNumber?, Network?, AnchoredAt?, Status(Pending|Confirmed|Failed)
  }
}

RevocationList {
  Id, TenantId, ListId,
  RevokedIndexes[],  -- bitstring status list
  UpdatedAt
}

IssuerProfile {
  TenantId, Name, Url, LogoUrl,
  Ed25519PublicKeyJwk,  -- for JWKS endpoint
  LinkedInOrgId?,
  BlockchainEnabled, BlockchainNetwork?
}
```

---

## WP 4.4 — Engagement & Wellbeing AI

**Sprint:** 24–26 | **Owner:** AI Team | **Effort:** 2w  
**Service:** `LMS.EngagementService`

### Context

The Engagement AI passively monitors learner interaction patterns during video playback and quiz sessions to detect signals of frustration (repeated replays, long pauses, rage-clicks) and boredom (fast-forwarding, tab switching, idle periods). When detected, it triggers adaptive responses: adjusting content pacing, suggesting a break, or notifying the instructor. This is entirely opt-in — learners are informed and can disable monitoring.

### User stories

**US-4.4.1** — As a student who is frustrated with content, the platform detects this and offers me a simpler explanation or suggests I take a break so that I don't give up.

**US-4.4.2** — As a student who is bored with content that is too easy, the platform suggests I skip ahead or try the advanced version so that I am challenged appropriately.

**US-4.4.3** — As a student, I am clearly informed that engagement signals are monitored and I can opt out at any time so that I trust the platform.

**US-4.4.4** — As an instructor, I receive a weekly digest showing where students are frustrated or bored in my content so that I can improve those sections.

**US-4.4.5** — As a student who appears frustrated, the AI tutor proactively offers help without me having to ask so that support feels natural.

### Acceptance criteria

**Client-side signal collection:**

- [ ] JavaScript engagement tracker embedded in video player and quiz renderer (opt-in only; consent stored in `UserConsent` table)
- [ ] Video signals collected (batched, sent every 30s to `POST /api/engagement/signals`):
  - `replay`: video rewound > 10 seconds
  - `pause_long`: paused > 60 seconds
  - `seek_forward`: skipped forward > 30 seconds (boredom signal)
  - `tab_hidden`: browser tab became hidden
  - `idle`: no mouse/keyboard activity > 120 seconds
  - `playback_rate`: changed playback speed (>1.5x = boredom; <0.8x = struggle)
- [ ] Quiz signals collected:
  - `answer_change`: changed answer before submitting (uncertainty)
  - `time_on_question`: time spent per question (long = struggle)
  - `rapid_submit`: answered all questions in < 20% of time limit (boredom / rushing)
- [ ] All signals include: `userId`, `contentId`, `sessionId`, `signalType`, `value`, `clientTimestamp`
- [ ] No audio, video, or screen capture ever collected — behavioural signals only

**Signal processing:**

- [ ] `POST /api/engagement/signals` — accepts batched signals array; stores in `EngagementSignal` table; triggers async scoring
- [ ] `EngagementScoringWorker`: consumes signals; computes per-session frustration score (0–1) and boredom score (0–1) using weighted rule set:

| Signal | Frustration weight | Boredom weight |
|---|---|---|
| `replay` | +0.3 | 0 |
| `pause_long` | +0.2 | 0 |
| `seek_forward` | 0 | +0.3 |
| `tab_hidden` | +0.1 | +0.2 |
| `idle` | +0.1 | +0.2 |
| `playback_rate > 1.5x` | 0 | +0.4 |
| `playback_rate < 0.8x` | +0.2 | 0 |
| `answer_change > 3` | +0.25 | 0 |
| `time_on_question > 2x avg` | +0.2 | 0 |
| `rapid_submit` | 0 | +0.3 |

- [ ] Thresholds: frustration score > 0.6 → `FrustrationDetected` event; boredom score > 0.6 → `BoredomDetected` event
- [ ] Events de-duplicated per session: only one frustration/boredom event per 15-minute window per session

**Adaptive responses:**

On `FrustrationDetected`:
- [ ] In-app gentle prompt: "Having trouble? Here's a simpler explanation →" (links to AI Tutor for that lesson)
- [ ] If AI Tutor (WP 4.2) is available: proactively opens a tutor session with pre-loaded context: "The student appears to be struggling with {lessonTitle}. Offer a simplified explanation."
- [ ] If frustration persists for 3 consecutive replays of same segment: suggest "Take a 5-minute break" with a dismissible banner
- [ ] Difficulty pacing: publish `FrustrationDetected` → RecommendationService evaluates whether to insert a remedial lesson into the path

On `BoredomDetected`:
- [ ] In-app prompt: "You're moving fast! Want to skip to the next lesson?" with Skip / Continue buttons
- [ ] If student agrees: ProgressService marks lesson complete (watchPercent = 100%); navigates to next lesson
- [ ] Suggest stretch goal: "Try the advanced version of this topic" if available in recommendation engine

**Instructor digest:**

- [ ] Weekly batch job aggregates engagement signals per lesson per course
- [ ] `GET /api/engagement/instructor/courses/{courseId}/insights` — instructor; returns `{ lessons[]{lessonId, title, avgFrustrationScore, avgBoredomScore, frustrationSpikes[]{positionSeconds, count}, topReplaySegments[]{startSeconds, replayCount}} }`
- [ ] NotificationWorker sends weekly email digest to instructors with: top 3 frustration points, top 3 boredom points, suggested actions
- [ ] "Suggested action" text generated by LLM call: "At 4:32 in Lesson 3, 23% of students replayed the segment 3+ times. Consider adding a visual diagram or additional explanation here."

**Consent & privacy:**

- [ ] Consent banner shown to student on first video/quiz load: "We monitor engagement signals to personalise your experience. [Learn more] [Accept] [Decline]"
- [ ] `POST /api/engagement/consent` — stores `{ userId, tenantId, consented: bool, consentedAt }`
- [ ] If `consented = false`: signals are not collected; no tracking code runs
- [ ] `DELETE /api/engagement/signals/me` — student can delete all their stored signals at any time (GDPR right to erasure integration)
- [ ] Engagement data never shared across tenants; never used for external advertising

### Key data model

```
UserConsent { UserId, TenantId, ConsentType(EngagementTracking), Consented, ConsentedAt, RevokedAt? }
EngagementSignal { Id, UserId, TenantId, ContentId, SessionId, SignalType, Value, ClientTimestamp, ReceivedAt }
EngagementScore { Id, UserId, TenantId, ContentId, SessionId, FrustrationScore, BoredomScore, ScoredAt }
EngagementInsight { Id, ContentId, TenantId, WeekOf, AvgFrustration, AvgBoredom, FrustrationSpikes[], TopReplaySegments[], GeneratedAt }
```

---

## WP 10.5 — AI Translation Service

**Sprint:** 26–28 | **Owner:** AI Team | **Effort:** 1.5w  
**Service:** `LMS.TranslationService`

### Context

The AI Translation Service allows instructors to publish multilingual versions of their courses. Lesson text and video captions are machine-translated by an LLM. The instructor reviews translations before publishing. Video captions in translated languages are generated by Whisper's translation mode (speech-to-translated-text). This builds on the caption infrastructure from Phase 3 (WP 4.6).

### User stories

**US-10.5.1** — As an instructor, I can request an AI translation of my course into any supported language so that I reach learners worldwide.

**US-10.5.2** — As an instructor, I can review and edit each translated lesson before it is published so that quality is maintained.

**US-10.5.3** — As a student, I can switch a course to my preferred language if a translation is available so that I learn in the language I am most comfortable in.

**US-10.5.4** — As an instructor, video captions are automatically translated alongside lesson text so that the full lesson experience is multilingual.

**US-10.5.5** — As a student, I can see which languages a course is available in before enrolling so that I choose the right version.

### Acceptance criteria

**Translation request:**

- [ ] `POST /api/translations/courses/{courseId}` — instructor requests translation: `{ targetLanguage(BCP 47 code, e.g. "vi", "ar", "fr"), includeVideoTranslation(bool) }`; returns `{ jobId }`; async processing
- [ ] Supported languages (Phase 4): `vi`, `ar`, `fr`, `de`, `ja`, `ko`, `pt`, `es`, `zh-Hans` (expandable via config)
- [ ] `GET /api/translations/jobs/{jobId}` — poll status: `Queued → Translating → Review → Published → Failed`
- [ ] Cost estimate returned before job starts: `{ estimatedTokens, estimatedCostUsd }` — instructor confirms before proceeding

**Translation pipeline:**

- [ ] For each lesson in the course (parallel, max 5 concurrent):
  1. Extract lesson markdown body from CourseService
  2. Call LLM API (GPT-4 or Claude) with system prompt: "Translate the following educational content from {sourceLanguage} to {targetLanguage}. Preserve all markdown formatting, code blocks, and technical terms. Do not translate code samples."
  3. Store translated body in `LessonTranslation { LessonId, Language, TranslatedBody, Status(Draft|Approved), TranslatedAt }`
- [ ] For each video lesson (if `includeVideoTranslation = true`):
  1. Fetch source VTT captions from ContentService (Phase 3 WP 4.6)
  2. If source VTT exists: call LLM to translate VTT segment-by-segment (preserving timestamps)
  3. If source VTT missing: run Whisper translation mode (`task=translate`) on original audio → produces English first, then translate to target
  4. Store translated VTT in S3; link to `ContentItemCaption { ContentId, Language, VttS3Key }`

**Instructor review:**

- [ ] `GET /api/translations/courses/{courseId}/review?language={lang}` — returns all lesson translations with `status` and side-by-side original + translation
- [ ] `PUT /api/translations/lessons/{lessonId}?language={lang}` — instructor edits translated body: `{ translatedBody }`
- [ ] `POST /api/translations/lessons/{lessonId}/approve?language={lang}` — marks lesson translation as approved
- [ ] `POST /api/translations/courses/{courseId}/publish?language={lang}` — only allowed when all lesson translations are `Approved`; creates a `CourseLocale` record
- [ ] Instructor cannot publish a translation until 100% of lessons are approved (enforced server-side)

**Student experience:**

- [ ] `GET /api/courses/{id}/languages` — returns available published locales: `[{ language, publishedAt }]`; accessible without auth
- [ ] Student selects language in course player; preference stored in `UserCourseLocalePreference { UserId, CourseId, Language }`
- [ ] Lesson body served in selected language if `CourseLocale` exists and is published; falls back to source language
- [ ] Video captions served in selected language if translated VTT exists; falls back to source language captions
- [ ] Course search (Marketplace Elasticsearch) indexes translated titles and descriptions for discoverability in other languages

**Quality safeguards:**

- [ ] LLM translation prompt includes: "Do not hallucinate or add information. Translate faithfully. Preserve all markdown."
- [ ] Translation diff view: UI shows word-level changes between original and translation for instructor review
- [ ] Back-translation check (optional per tenant): after translation, call LLM to translate back to source language and compute BLEU score; warn instructor if BLEU < 0.7
- [ ] `TranslationLog { JobId, LessonId, Language, InputTokens, OutputTokens, ModelUsed, Cost, CreatedAt }` for cost tracking

### Key data model

```
TranslationJob { Id, CourseId, TenantId, InstructorId, TargetLanguage, IncludeVideo, Status, LessonsTotal, LessonsCompleted, LessonsApproved, TokensUsed, CostUsd, CreatedAt, CompletedAt }
LessonTranslation { Id, LessonId, CourseId, TenantId, Language, TranslatedBody, Status(Draft|Approved), InstructorEdited, TranslatedAt, ApprovedAt }
ContentItemCaption { Id, ContentItemId, TenantId, Language, VttS3Key, IsAutoGenerated, CreatedAt }
CourseLocale { CourseId, TenantId, Language, PublishedAt, TranslationJobId }
UserCourseLocalePreference { UserId, CourseId, Language }
```

---

## Cross-cutting Phase 4 Concerns

### Performance & cost governance

Phase 4 introduces three cost-intensive capabilities: virtual labs (Kubernetes compute), blockchain anchoring (gas fees), and AI translation (LLM tokens). The following governance measures must be in place before any Phase 4 feature goes to production:

- [ ] **Lab cost controls:** per-tenant monthly lab-hour quota (configurable); `LabQuotaExceeded` event stops new session provisioning; admin alerted at 80% quota
- [ ] **AI translation budget:** per-tenant monthly token budget; cost estimate shown before each translation job; `TranslationBudgetExceeded` blocks new jobs
- [ ] **Blockchain gas fund:** platform maintains a gas wallet on Polygon; wallet balance monitored; alert at < 0.1 MATIC; anchor queue pauses if balance insufficient
- [ ] **Engagement signal storage:** signals older than 90 days are archived to S3 (cold storage) and purged from the primary DB; ClickHouse aggregates retained for 2 years

### Feature flag requirements

All Phase 4 features must be behind feature flags (established in Phase 1 WP 1.7) and disabled by default:

| Feature flag | Default | Controls |
|---|---|---|
| `VirtualLabs` | Disabled | Lab provisioning endpoints; lab UI |
| `BlockchainAnchoring` | Disabled | Blockchain anchoring per tenant |
| `OpenBadges3` | Enabled | OB3 credential issuance (replaces basic cert) |
| `EngagementTracking` | Disabled | Signal collection, consent banner |
| `AiTranslation` | Disabled | Translation job creation |

### Security hardening for Phase 4

- [ ] **Lab isolation:** each lab pod runs as non-root user (`runAsNonRoot: true`); read-only root filesystem (`readOnlyRootFilesystem: true`); no privilege escalation (`allowPrivilegeEscalation: false`)
- [ ] **Lab network:** egress blocked to all external internet from lab pods (prevents data exfiltration, cryptomining); DNS allowed only
- [ ] **Credential private key:** Ed25519 signing key stored in Azure Key Vault; never in environment variables or config files; accessed via Managed Identity
- [ ] **Translation PII:** lesson content sent to LLM API must be scrubbed of any student-identifiable data before transmission; only course content (not student work) is translated
- [ ] **Engagement signals:** signals stored with `userId` hash (not raw UUID) in analytics tables; re-linkable only via lookup table accessible to compliance roles only

---

## Open Design Decisions

| ID | Decision | Blocks | Options | Recommendation |
|---|---|---|---|---|
| OD-10.2.a | Lab gateway: YARP vs Envoy vs Nginx per pod | WP 10.2 | YARP / Envoy / Nginx sidecar | YARP in dedicated lab-gateway deployment; avoids per-pod sidecar overhead |
| OD-10.2.b | Lab storage: ephemeral vs PVC per session | WP 10.2 | Ephemeral / PVC | Ephemeral for Phase 4; PVC in Phase 5 for stateful multi-session labs |
| OD-10.2.c | Docker image source: shared registry vs per-tenant | WP 10.2 | Shared with tenant namespace / Per-tenant private | Shared platform registry with tenant prefix; private registry as enterprise add-on |
| OD-10.3.a | Blockchain network: Polygon mainnet vs testnet for launch | WP 10.3 | Mainnet (real cost) / Amoy testnet (free) | Amoy testnet for Phase 4 launch; mainnet migration when demand proven |
| OD-10.3.b | Credential expiry: credentials expire or not? | WP 10.3 | No expiry / Configurable expiry per course | No expiry by default; optional expiry (e.g., 3-year compliance cert) configurable per course |
| OD-10.3.c | LinkedIn org ID: per tenant or single platform org? | WP 10.3 | Per tenant / Single platform | Per tenant — enterprise customers want credentials to appear from their organisation |
| OD-4.4.a | Engagement consent: opt-in or opt-out? | WP 4.4 | Opt-in (explicit accept) / Opt-out (default on, can disable) | Opt-in always — privacy-first, avoids GDPR complexity; slightly lower data coverage acceptable |
| OD-4.4.b | Frustration response: automatic AI tutor launch or just a prompt? | WP 4.4 | Auto-launch tutor / Show prompt only | Show prompt first; auto-launch only if student has interacted with tutor before (lower friction for returning users) |
| OD-10.5.a | Translation LLM: same provider as grading (WP 3.3) or separate? | WP 10.5 | Same provider / Separate | Same configurable provider (WP 3.3 OD-3.3.a decision applies); reduces key management complexity |
| OD-10.5.b | Back-translation BLEU check: mandatory or optional? | WP 10.5 | Mandatory gate / Optional warning | Optional warning — BLEU is imperfect; instructor review is the real quality gate |
| OD-10.5.c | Video translation: Whisper translate mode vs LLM translation of existing VTT? | WP 10.5 | Whisper translate / LLM VTT translation | LLM VTT translation if source VTT exists (preserves timing); Whisper translate as fallback (no source VTT) |

---

## Phase 4 Event Contract Summary

| Event | Publisher | Consumers |
|---|---|---|
| `LabSessionReady` | VirtualLabService | NotificationWorker (student alert) |
| `LabCompleted` | VirtualLabService | ProgressService, GamificationService, AnalyticsWorker |
| `LabSessionTerminated` | VirtualLabService | AnalyticsWorker |
| `CredentialIssued` | CertificateService | AnchoringWorker, NotificationWorker (LinkedIn prompt) |
| `CredentialRevoked` | CertificateService | NotificationWorker (student), AuditLogService |
| `BlockchainAnchorConfirmed` | AnchoringWorker | CertificateService (update anchor status), NotificationWorker |
| `BlockchainAnchorFailed` | AnchoringWorker | NotificationWorker (platform admin alert) |
| `FrustrationDetected` | EngagementService | AiTutorService (proactive session), RecommendationService, AnalyticsWorker |
| `BoredomDetected` | EngagementService | RecommendationService (stretch goal), AnalyticsWorker |
| `TranslationJobCompleted` | TranslationService | NotificationWorker (instructor review prompt) |
| `CourseLocalePublished` | TranslationService | MarketplaceService (re-index with new languages), AnalyticsWorker |

---

## Phase 4 API Surface Summary

| Service | Base path | Auth model | Notes |
|---|---|---|---|
| VirtualLabService | `/api/labs` | JWT; enrollment required for sessions; admin for resources | Lab proxy at `/labs/proxy/{sessionId}/` |
| CertificateService (ext) | `/api/certificates`, `/credentials/{id}` | JWT for wallet; public for verify | JWKS at `/.well-known/jwks.json` |
| EngagementService | `/api/engagement` | JWT; consent required for signals; instructor for insights | Client-side SDK for signal collection |
| TranslationService | `/api/translations` | JWT; instructor only for job creation | Async job pattern; SSE for progress |

---

## Full Platform — Consolidated Open Decisions

The following is the complete list of all open design decisions across all four phases. Use this as the agenda for architecture review sessions before each phase begins.

### Must resolve before Phase 1 Sprint 1

| ID | Decision | Recommendation |
|---|---|---|
| OD-1.1.a | Single Keycloak realm vs per-tenant realm | Single realm + `tenant_id` claim; per-tenant deferred to Phase 3 |
| OD-1.1.b | MFA enforcement for instructors | Required for instructors; optional for students |
| OD-2.3.a | Video transcoding engine | FFmpeg on k8s Job; MediaConvert as production option |
| OD-2.3.b | Video progress ownership (ContentService vs ProgressService) | ContentService = raw position; ProgressService = completion % |
| OD-2.2.a | Course version granularity | Snapshot on every publish; enrolled users pinned |
| OD-2.4.a | Free vs paid courses in Phase 1 | `isFree` flag on Course; paid enrollment stubbed |
| OD-3.1.a | Assessment tied to lesson or course | Both; `lessonId` nullable |
| OD-7.5.a | PDF generation library | QuestPDF |

### Must resolve before Phase 2 Sprint 9

| ID | Decision | Recommendation |
|---|---|---|
| OD-5.1.a | Cross-tenant global leaderboard | Tenant-scoped default; global opt-in per tenant |
| OD-5.2.a | Streak timezone: UTC vs user local | User local timezone from profile |
| OD-5.5.a | Badge icon hosting | Separate static CDN (public assets) |
| OD-6.1.a | Default live session provider | Both Zoom + Google Meet configurable; Zoom default |
| OD-6.3.a | Forum AI pre-moderation | Deferred to Phase 3; instructor-only for Phase 2 |
| OD-7.1.a | Subscription billing: per-user vs org bulk | Per-user for Phase 2; org bulk in Phase 3 |
| OD-8.1.a | Analytics store | ClickHouse; TimescaleDB if team unfamiliar |
| OD-3.3.a | LLM provider | Configurable; Anthropic Claude as default |
| OD-3.4.a | Plagiarism provider | Copyleaks; Turnitin as enterprise option |
| OD-4.2.a | RAG chunk size | 500 tokens with 50-token overlap |
| OD-4.2.b | AI Tutor mode | Socratic default; student can toggle after 2 exchanges |
| OD-9.3.a | SCORM iframe security | Sandboxed iframe + postMessage bridge |

### Must resolve before Phase 3 Sprint 17

| ID | Decision | Recommendation |
|---|---|---|
| OD-7.2.a | Marketplace search engine | Elasticsearch; Typesense as lighter alternative |
| OD-7.2.b | Marketplace approval role | Dedicated `curator` role in Keycloak |
| OD-7.3.a | Minimum payout threshold | Configurable per tenant; default $25 |
| OD-7.6.a | Skill taxonomy seed | SFIA 9 seed + org customisation |
| OD-6.4.a | Peer review scoring outlier detection | Drop scores >2σ from mean |
| OD-9.1.a | GDPR forum post erasure | Replace body with "[Deleted]" |
| OD-9.1.b | Payment record anonymisation | Retain amount/date; anonymise name/email |
| OD-9.4.a | PDF watermark type | Configurable; invisible default |
| OD-9.5.a | TLS cert provisioning | cert-manager + Let's Encrypt |
| OD-10.1.a | Offline video caching | Full download (explicit); progressive cache for auto |
| OD-10.4.a | RTL implementation | CSS logical properties throughout |

### Must resolve before Phase 4 Sprint 24

| ID | Decision | Recommendation |
|---|---|---|
| OD-10.2.a | Lab gateway | YARP in dedicated deployment |
| OD-10.2.b | Lab storage | Ephemeral for Phase 4 |
| OD-10.2.c | Docker image registry | Shared platform registry with tenant prefix |
| OD-10.3.a | Blockchain network | Polygon Amoy testnet for Phase 4; mainnet later |
| OD-10.3.b | Credential expiry | No expiry default; optional per course |
| OD-10.3.c | LinkedIn org ID | Per tenant |
| OD-4.4.a | Engagement consent model | Opt-in always |
| OD-4.4.b | Frustration response | Prompt first; auto-launch for returning tutor users |
| OD-10.5.a | Translation LLM | Same as grading provider (WP 3.3) |
| OD-10.5.b | BLEU check | Optional warning only |
| OD-10.5.c | Video translation method | LLM VTT translation; Whisper as fallback |

---

## Full Platform — Complete Event Catalog

All events defined in `LMS.Contracts`. Every event includes `TenantId` and `OccurredAt` fields (omitted below for brevity).

### Identity & enrollment

| Event | Publisher | Key fields |
|---|---|---|
| `UserRegistered` | IdentityService | UserId, Email |
| `UserDeactivated` | IdentityService | UserId, DeactivatedBy |
| `UserEnrolled` | EnrollmentService | UserId, CourseId, PlanType |
| `EnrollmentSuspended` | EnrollmentService | UserId, CourseId, Reason |

### Content & learning

| Event | Publisher | Key fields |
|---|---|---|
| `CoursePublished` | CourseService | CourseId, InstructorId, Version |
| `LessonCompleted` | ProgressService | UserId, LessonId, CourseId, WatchPercent |
| `CourseCompleted` | ProgressService | UserId, CourseId |
| `ContentProcessingCompleted` | ContentService | ContentItemId, HlsManifestUrl |
| `ContentProcessingFailed` | ContentService | ContentItemId, Error |
| `ContentCaptioned` | ContentService | ContentItemId, Language, VttUrl |
| `CourseLocalePublished` | TranslationService | CourseId, Language |

### Assessment

| Event | Publisher | Key fields |
|---|---|---|
| `AssessmentSubmitted` | AssessmentService | UserId, AssessmentId, CourseId, Score, Passed |
| `EssaySubmitted` | AssessmentService | UserId, SubmissionId, AssessmentId |
| `GradeReleased` | GradingWorker | UserId, SubmissionId, FinalScore |
| `PlagiarismFlagged` | PlagiarismWorker | SubmissionId, SimilarityScore, FlagLevel |
| `LabCompleted` | VirtualLabService | UserId, LabDefinitionId, SessionId |

### Gamification

| Event | Publisher | Key fields |
|---|---|---|
| `AchievementUnlocked` | GamificationService | UserId, BadgeId, BadgeName, Rarity |
| `LevelUp` | GamificationService | UserId, NewLevel, TotalXp |
| `StreakMaintained` | GamificationService | UserId, CurrentStreak |
| `StreakBroken` | GamificationService | UserId, StreakLength |
| `TaskCompleted` | GamificationService | UserId, TaskDefinitionId |
| `GoalCompleted` | GamificationService | UserId, GoalId |

### Live & social

| Event | Publisher | Key fields |
|---|---|---|
| `LiveSessionAttended` | LiveSessionService | UserId, SessionId, CourseId, DurationMinutes |
| `PeerReviewCompleted` | PeerReviewService | UserId, SubmissionId, AggregateScore |
| `MentorMatchClosed` | CohortService | MentorId, MenteeId, CourseId, Rating |

### Payments & credentials

| Event | Publisher | Key fields |
|---|---|---|
| `PaymentProcessed` | PaymentService | UserId, CourseId?, Amount, Currency |
| `PaymentFailed` | PaymentService | UserId, Reason |
| `SubscriptionCancelled` | PaymentService | UserId, PlanId |
| `CredentialIssued` | CertificateService | UserId, CourseId, CredentialId |
| `CredentialRevoked` | CertificateService | CredentialId, Reason |
| `BlockchainAnchorConfirmed` | AnchoringWorker | CredentialId, TxHash, Network |

### Engagement & AI

| Event | Publisher | Key fields |
|---|---|---|
| `FrustrationDetected` | EngagementService | UserId, ContentId, SessionId, Score |
| `BoredomDetected` | EngagementService | UserId, ContentId, SessionId, Score |
| `SpacedRepetitionDue` | RecommendationService | UserId, TopicSlug, NextReviewAt |
| `LearnerAtRisk` | AnalyticsWorker | UserId, CourseId, RiskScore, Factors[] |
| `GenerationJobCompleted` | AiCourseGeneratorService | JobId, InstructorId, CourseId |
| `TranslationJobCompleted` | TranslationService | JobId, CourseId, Language |

### Compliance & platform

| Event | Publisher | Key fields |
|---|---|---|
| `GdprErasureRequested` | GdprService | UserId, RequestId |
| `UserDataErased` | Each service | UserId, ServiceName, RequestId |
| `TrainingOverdue` | ComplianceService | UserId, AssignmentId, DaysOverdue |
| `AuditEvent` | All services | ActorId, Action, ResourceType, ResourceId |
| `LabSessionTerminated` | VirtualLabService | SessionId, UserId, Reason, DurationMinutes |

---

## Full Platform — Complete Service Inventory

Final inventory of all services in `LMS.AppHost` at end of Phase 4.

| # | Service | Database | Bus role | Phase introduced |
|---|---|---|---|---|
| 1 | `LMS.Gateway` (YARP) | Redis | — | Phase 1 |
| 2 | `LMS.IdentityService` | PostgreSQL | Publisher | Phase 1 |
| 3 | `LMS.CourseService` | PostgreSQL | Publisher | Phase 1 |
| 4 | `LMS.ContentService` | MongoDB + S3 | Publisher | Phase 1 |
| 5 | `LMS.EnrollmentService` | PostgreSQL | Publisher/Consumer | Phase 1 |
| 6 | `LMS.ProgressService` | PostgreSQL | Publisher/Consumer | Phase 1 |
| 7 | `LMS.AssessmentService` | PostgreSQL + Redis | Publisher | Phase 1 |
| 8 | `LMS.CertificateService` | PostgreSQL + S3 | Publisher/Consumer | Phase 1 → extended Phase 4 |
| 9 | `LMS.NotificationWorker` | — | Consumer | Phase 1 |
| 10 | `LMS.GamificationService` | PostgreSQL + Redis | Publisher/Consumer | Phase 2 |
| 11 | `LMS.LiveSessionService` | PostgreSQL | Publisher | Phase 2 |
| 12 | `LMS.ForumService` | PostgreSQL + Redis | Publisher | Phase 2 |
| 13 | `LMS.PaymentService` | PostgreSQL | Publisher | Phase 2 |
| 14 | `LMS.AnalyticsWorker` | ClickHouse | Consumer | Phase 2 |
| 15 | `LMS.GradingWorker` | PostgreSQL | Publisher/Consumer | Phase 2 |
| 16 | `LMS.PlagiarismWorker` | PostgreSQL + pgvector | Publisher/Consumer | Phase 2 |
| 17 | `LMS.RecommendationService` | PostgreSQL + pgvector | Publisher/Consumer | Phase 2 |
| 18 | `LMS.AiTutorService` | PostgreSQL + pgvector | Consumer | Phase 2 |
| 19 | `LMS.MarketplaceService` | PostgreSQL + Elasticsearch | Publisher | Phase 3 |
| 20 | `LMS.SkillsService` | PostgreSQL | Publisher/Consumer | Phase 3 |
| 21 | `LMS.PeerReviewService` | PostgreSQL | Publisher | Phase 3 |
| 22 | `LMS.CohortService` | PostgreSQL | Publisher | Phase 3 |
| 23 | `LMS.AiCourseGeneratorService` | PostgreSQL | Publisher | Phase 3 |
| 24 | `LMS.GdprService` | PostgreSQL | Publisher/Consumer | Phase 3 |
| 25 | `LMS.AuditLogService` | EventStoreDB | Consumer | Phase 3 |
| 26 | `LMS.TenantService` | PostgreSQL | Publisher | Phase 3 |
| 27 | `LMS.ComplianceService` | PostgreSQL | Publisher/Consumer | Phase 3 |
| 28 | `LMS.RevenueWorker` | PostgreSQL | Consumer | Phase 3 |
| 29 | `LMS.VirtualLabService` | PostgreSQL | Publisher | Phase 4 |
| 30 | `LMS.AnchoringWorker` | PostgreSQL | Publisher/Consumer | Phase 4 |
| 31 | `LMS.EngagementService` | PostgreSQL + ClickHouse | Publisher | Phase 4 |
| 32 | `LMS.TranslationService` | PostgreSQL | Publisher | Phase 4 |

**Total: 32 services** across 4 phases.  
**Shared infrastructure:** PostgreSQL · MongoDB · Redis · RabbitMQ · ClickHouse · Elasticsearch · EventStoreDB · S3 · CDN · Kubernetes

---

*Document: LMS Phase 4 Feature Specification · Version 1.0 · April 2026*  
*This completes the full four-phase feature specification for the LMS platform.*  
*All four phase documents together form the complete product specification context for implementation.*
