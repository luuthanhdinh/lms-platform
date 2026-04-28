# LMS Platform — Work Breakdown Structure, Roadmap & Implementation Plan

**Stack:** Keycloak SSO · .NET Aspire Microservices · YARP · MassTransit · RabbitMQ · PostgreSQL · Redis · MongoDB · Semantic Kernel  
**Version:** 1.0 · April 2026  
**Classification:** Working context document

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Work Breakdown Structure](#2-work-breakdown-structure-wbs)
3. [Roadmap — Sprint Gantt](#3-roadmap--sprint-gantt)
4. [Detailed Phase Implementation Plan](#4-detailed-phase-implementation-plan)
5. [Team Structure & Velocity](#5-team-structure--velocity)
6. [Risk Register](#6-risk-register)
7. [Definition of Done](#7-definition-of-done)
8. [Success Metrics & KPIs](#8-success-metrics--kpis)

---

## 1. Executive Summary

This document is the complete WBS, roadmap, and project plan for the LMS platform — a cloud-native Learning Management System built on .NET Aspire microservices with Keycloak as the identity and SSO provider.

The platform is designed across **four phases** spanning approximately **24 two-week sprints (12 months)**. It covers **10 major feature domains** comprising **60+ individual work packages**, progressing from foundational infrastructure through core learning services, AI-powered capabilities, enterprise compliance, and advanced differentiating features.

### 1.1 Scope overview

- 10 major work breakdown domains (WBS Level 1)
- 60+ individual work packages across 4 delivery phases
- Team of 11 FTEs across 7 roles
- Technology stack: .NET 9, Aspire, YARP, Keycloak, MassTransit, RabbitMQ, PostgreSQL, Redis, MongoDB, Semantic Kernel
- Delivery model: 2-week sprints, event-driven microservices, feature-flagged rollout

### 1.2 Phase summary

| Phase | Name | Key deliverables | Duration | Sprints |
|---|---|---|---|---|
| Phase 1 | Foundation & Core | Infrastructure, SSO, core learning services, basic assessment, notifications | Q1–Q2 (16w) | S1–S8 |
| Phase 2 | Learning Intelligence | Gamification, adaptive assessment, AI tutor, live sessions, payments, analytics | Q3–Q4 (16w) | S9–S16 |
| Phase 3 | Enterprise & Scale | Marketplace, skills, peer learning, GDPR, white-label, DRM, predictive AI | Q5–Q6 (14w) | S17–S23 |
| Phase 4 | Innovation | Virtual labs, blockchain credentials, engagement AI, AI translation | Q7–Q8 (4w) | S24+ |

---

## 2. Work Breakdown Structure (WBS)

Each work package (WP) is defined with a unique ID, name, primary deliverable, estimated effort, responsible role, and target phase.  
**Effort** = calendar weeks for a single engineer unless noted.

### 1.0 Infrastructure & Foundation

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 1.1 | Keycloak SSO setup | Realm, clients, OIDC config | 2w | DevOps | Phase 1 |
| 1.2 | Aspire AppHost scaffold | Solution structure, CI/CD | 1w | Lead Dev | Phase 1 |
| 1.3 | YARP API Gateway | Routing, JWT middleware | 1w | Backend | Phase 1 |
| 1.4 | Service defaults & telemetry | OpenTelemetry, health checks | 1w | Lead Dev | Phase 1 |
| 1.5 | Database provisioning | PostgreSQL, Redis, MongoDB | 1w | DevOps | Phase 1 |
| 1.6 | Event bus (RabbitMQ) | MassTransit, contracts lib | 1w | Backend | Phase 1 |
| 1.7 | Feature flags | Azure App Config integration | 0.5w | Lead Dev | Phase 1 |

### 2.0 Core Learning Services

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 2.1 | Identity service | Auth, roles, multi-tenant | 2w | Backend | Phase 1 |
| 2.2 | Course service | CRUD, catalog, versioning | 3w | Backend | Phase 1 |
| 2.3 | Content delivery service | Video, PDF, S3, CDN, HLS | 3w | Backend | Phase 1 |
| 2.4 | Enrollment service | Seats, waitlist, billing hook | 2w | Backend | Phase 1 |
| 2.5 | Progress service (xAPI) | Tracking, completion, xAPI | 3w | Backend | Phase 1 |
| 2.6 | Notification worker | Email, push, SMS via bus | 1.5w | Backend | Phase 1 |

### 3.0 Assessment & Grading

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 3.1 | Assessment service | Quiz engine, question bank | 3w | Backend | Phase 1 |
| 3.2 | Adaptive assessment engine | IRT theta, difficulty bands | 2w | Backend | Phase 2 |
| 3.3 | LLM auto-grading | Short answer + rubric AI | 2.5w | AI Team | Phase 2 |
| 3.4 | Plagiarism detection | pgvector + Copyleaks webhook | 2w | Backend | Phase 2 |
| 3.5 | Grade appeal workflow | Audit trail, instructor review | 1w | Backend | Phase 2 |

### 4.0 AI-Powered Learning

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 4.1 | Recommendation service | Knowledge graph, mastery, CF | 3w | AI Team | Phase 2 |
| 4.2 | AI tutor / RAG chatbot | Semantic Kernel, pgvector | 3w | AI Team | Phase 2 |
| 4.3 | AI course generator | Outline → lessons → quizzes | 3w | AI Team | Phase 3 |
| 4.4 | Engagement AI | Frustration/boredom detection | 2w | AI Team | Phase 4 |
| 4.5 | AI-assisted content authoring | Block editor, quiz generator | 2w | AI Team | Phase 3 |
| 4.6 | Auto caption (Whisper) | Video caption generation | 1w | AI Team | Phase 3 |

### 5.0 Gamification

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 5.1 | Points & XP engine | Leaderboard, Redis sorted set | 1.5w | Backend | Phase 2 |
| 5.2 | Streak & check-in system | Freeze tokens, grace period | 1w | Backend | Phase 2 |
| 5.3 | Tasks & daily missions | Repeatable, deadline-aware | 1w | Backend | Phase 2 |
| 5.4 | Goals (multi-step) | Progress bar, instructor-set | 1w | Backend | Phase 2 |
| 5.5 | Badges & achievements | Rules engine, rarity tiers | 1.5w | Backend | Phase 2 |
| 5.6 | SignalR live badge pop-ups | Real-time frontend events | 0.5w | Frontend | Phase 2 |

### 6.0 Social & Live Learning

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 6.1 | Live session service | Zoom webhook, attendance | 2w | Backend | Phase 2 |
| 6.2 | Recording ingestor | Download → ContentService | 1w | Backend | Phase 2 |
| 6.3 | Discussion forums | Threaded, per-course | 2w | Full-stack | Phase 2 |
| 6.4 | Peer review system | Rubric scoring, assignment | 2w | Full-stack | Phase 3 |
| 6.5 | Study groups & cohorts | Capped groups, shared goals | 1.5w | Full-stack | Phase 3 |
| 6.6 | Mentor pairing | Advanced + beginner matching | 1w | Backend | Phase 3 |

### 7.0 Business & Monetisation

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 7.1 | Payment service | Stripe subscriptions, plans | 2w | Backend | Phase 2 |
| 7.2 | Marketplace service | Course listing, ratings | 2.5w | Full-stack | Phase 3 |
| 7.3 | Revenue sharing | Stripe Connect, payouts | 1.5w | Backend | Phase 3 |
| 7.4 | Coupon & promotions engine | Flash sales, bundles | 1w | Backend | Phase 3 |
| 7.5 | Certificate service | Open Badges 3.0, verify URL | 2w | Backend | Phase 2 |
| 7.6 | Skills & competency framework | Taxonomy, skill passport | 2.5w | Backend | Phase 3 |

### 8.0 Analytics & Reporting

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 8.1 | Analytics worker (CQRS) | Event projections, ClickHouse | 2w | Backend | Phase 2 |
| 8.2 | Instructor dashboards | Completion, engagement funnels | 2w | Full-stack | Phase 2 |
| 8.3 | Predictive at-risk detection | ML model, early warning | 2.5w | AI Team | Phase 3 |
| 8.4 | Org-level BI & data export | S3 export, Power BI embed | 1.5w | Backend | Phase 3 |
| 8.5 | Skill gap analysis dashboard | Org admin reports | 1w | Full-stack | Phase 3 |

### 9.0 Compliance, Security & Enterprise

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 9.1 | GDPR service | Erasure, export API | 2w | Backend | Phase 3 |
| 9.2 | Audit log service | Immutable EventStore append | 1.5w | Backend | Phase 3 |
| 9.3 | SCORM/xAPI/cmi5 compliance | Standards adapter layer | 2w | Backend | Phase 2 |
| 9.4 | Content DRM | Signed URLs, watermark PDF | 1.5w | Backend | Phase 3 |
| 9.5 | White-label & multi-tenant | Custom domain, theme engine | 3w | Full-stack | Phase 3 |
| 9.6 | Mandatory training tracking | Deadlines, compliance alerts | 1w | Backend | Phase 3 |

### 10.0 Advanced Features

| ID | Work package | Primary deliverable | Effort | Owner | Phase |
|---|---|---|---|---|---|
| 10.1 | Offline / PWA mode | Service worker, sync engine | 2.5w | Full-stack | Phase 3 |
| 10.2 | Virtual lab service | k8s ephemeral containers | 3w | DevOps/Dev | Phase 4 |
| 10.3 | Digital credentials (blockchain) | Open Badges + chain anchor | 2w | Backend | Phase 4 |
| 10.4 | Accessibility & i18n | WCAG 2.1 AA, RTL, AI translate | 2w | Full-stack | Phase 3 |
| 10.5 | AI translation service | Course multilingual publish | 1.5w | AI Team | Phase 4 |

---

## 3. Roadmap — Sprint Gantt

**Convention:** Each sprint = 2 weeks. `█` = active development. Phase colours referenced below.

```
Phase 1 (S1–S8)   = Foundation & Core
Phase 2 (S9–S16)  = Learning Intelligence
Phase 3 (S17–S23) = Enterprise & Scale
Phase 4 (S24+)    = Innovation
```

| Feature stream | S1 | S2 | S3 | S4 | S5 | S6 | S7 | S8 | S9 | S10 | S11 | S12 | S13 | S14 | S15 | S16 | S17 | S18 | S19 | S20 | S21 | S22 | S23 | S24 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1. Infrastructure & Foundation    | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 2. Core Learning Services         | █ | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 3. Assessment & Grading (basic)   | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 4. Gamification engine            | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 5. Live sessions & forums         | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 6. Analytics worker (CQRS)        | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 7. Payment & Certificates         | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 8. Adaptive assessment (IRT)      | · | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 9. AI tutor / RAG chatbot         | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 10. Recommendation service        | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 11. Plagiarism detection           | · | · | · | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · | · | · | · | · | · |
| 12. Marketplace & revenue split    | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · | · |
| 13. Skills & competency           | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · |
| 14. Peer review & cohorts         | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · | · | · |
| 15. GDPR & compliance service     | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · | · |
| 16. White-label multi-tenant      | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | █ | · | · | · | · | · | · | · |
| 17. Content DRM & security        | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · | · | · |
| 18. Predictive at-risk AI         | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · | · |
| 19. AI course generator           | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · | · |
| 20. Offline PWA mode              | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | · | · | · | · | · |
| 21. Accessibility & i18n          | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | · | · | · | · | · | · |
| 22. Virtual lab service           | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ | █ | · |
| 23. Engagement / wellbeing AI     | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | · | · |
| 24. Blockchain credentials        | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ | █ |
| 25. AI translation service        | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | · | █ | █ | █ |

---

## 4. Detailed Phase Implementation Plan

### Phase 1 — Foundation & Core (Sprints 1–8)

**Goal:** deliver a working LMS skeleton — students can register, browse courses, watch content, track progress, and complete basic assessments. All infrastructure is production-ready from day one.

#### Sprint 1–2: Infrastructure

- Stand up Keycloak container in Aspire AppHost with realm-export.json auto-import
- Scaffold all Aspire projects: AppHost, ServiceDefaults, Contracts, Gateway (YARP)
- Configure OpenTelemetry → Aspire Dashboard; health check endpoints on all services
- Provision PostgreSQL (+ pgvector extension), Redis, MongoDB, RabbitMQ via Aspire containers
- Set up GitHub Actions CI: build → test → push → deploy to staging
- Feature flag baseline with Azure App Configuration

#### Sprint 3–4: Identity, Course & Enrollment

- Identity service: Keycloak JWT validation, role claims (student/instructor/admin/org-admin)
- Course service: catalog CRUD, module/lesson hierarchy, prerequisites, course versioning
- Enrollment service: seat management, waitlist, PaymentService hook (stubbed)
- YARP gateway: per-role route policies, X-User-Id / X-Roles header propagation

#### Sprint 5–6: Content Delivery & Progress

- Content service: S3 upload, MongoDB metadata, video transcoding pipeline (async HLS)
- CDN integration (CloudFront signed URLs, short TTL)
- Progress service: lesson completion tracking, xAPI statement emitter, watch-time events
- Notification worker: email templates (SendGrid), MassTransit consumer wired to all core events

#### Sprint 7–8: Assessment & Stabilisation

- Assessment service: question bank, quiz CRUD, timed session with Redis state, auto-grading (MCQ)
- Certificate worker: PDF generation on CourseCompleted event
- Integration test suite: per-service + cross-service scenario tests
- Load test baseline: k6 scripts for enrollment flow, content delivery, quiz submission
- Phase 1 review & bug fix sprint buffer

---

### Phase 2 — Learning Intelligence (Sprints 9–16)

**Goal:** elevate the platform with AI, gamification, live learning, payments, and analytics. The platform becomes meaningfully differentiated in this phase.

#### Sprint 9–10: Gamification & Payments

- GamificationService: points/XP engine, streak/check-in (Redis), tasks, goals, achievements rules engine
- Redis leaderboard (ZADD sorted sets); SignalR hub for live badge pop-ups
- PaymentService: Stripe subscriptions, webhook handler, plan tier enforcement in Enrollment
- SCORM 2004 / xAPI / cmi5 compliance adapter layer on ContentService

#### Sprint 11–12: Live Sessions & Analytics

- LiveSessionService: Zoom/Google Meet webhook handler, attendance tracker, recording ingestor
- AnalyticsWorker: CQRS event projections → ClickHouse (or TimescaleDB)
- Instructor dashboard: completion funnels, watch-time heatmaps, quiz score distributions
- Discussion forum service: threaded comments, per-course namespace, moderation queue

#### Sprint 13–14: Adaptive Assessment & Plagiarism

- Adaptive session engine: IRT theta tracking in Redis, difficulty band selector, question randomisation
- PlagiarismWorker: internal cosine similarity via pgvector; Copyleaks/Turnitin webhook integration
- Grade appeal workflow: instructor review queue, audit trail, student notification on decision

#### Sprint 15–16: AI Tutor & Recommendation

- RecommendationService: knowledge graph per learner (PostgreSQL adjacency list), mastery scoring, spaced repetition scheduler
- AI Tutor: Semantic Kernel RAG pipeline; per-course vector namespace in pgvector; Socratic mode prompt chain
- LLM auto-grading: short-answer rubric scoring via Anthropic/OpenAI API; instructor review gate before release
- Phase 2 review, performance tuning, A/B test setup for recommendation quality

---

### Phase 3 — Enterprise & Scale (Sprints 17–23)

**Goal:** unlock B2B and enterprise sales. Multi-tenancy, marketplace, compliance, and advanced analytics make the platform enterprise-grade.

#### Sprint 17–18: Marketplace & Revenue

- MarketplaceService: course listing, search (Elasticsearch), ratings/reviews, curator approval flow
- Revenue sharing: Stripe Connect instructor accounts, configurable split, nightly payout worker
- Coupon/promotions engine: flash sales, bundle pricing, org-bulk licensing
- SkillsService: SFIA-compatible taxonomy, course-to-skill mapping, learner skill passport (JSON-LD)

#### Sprint 19–20: Peer Learning & Social

- Peer review system: rubric builder, blind/open assignment modes, reviewer assignment algorithm
- Study groups: learner-created, capped, shared goal tracking, group leaderboard
- Mentor pairing: matching algorithm (mastery score delta), session scheduling, feedback loop
- AI course generator: outline → lesson → quiz pipeline; instructor review & publish gate

#### Sprint 21–22: Compliance & Security

- GDPR service: right-to-erasure (soft delete + anonymisation), data export API (JSON/CSV)
- Audit log service: append-only EventStoreDB projection; SOC 2 evidence S3 bucket
- Content DRM: signed URL rotation, PDF watermarking (user ID embedded), device-limit enforcement
- White-label: per-tenant custom domain (automated SSL via cert-manager), theme engine (logo/palette/font)
- Mandatory training tracking: deadline manager, compliance dashboard, auto-escalation alerts

#### Sprint 23: AI & Accessibility

- Predictive at-risk detection: ML model (completion rate + engagement + score trends) → instructor nudge
- WCAG 2.1 AA audit + remediation across all frontends
- Auto-caption generation: Whisper model deployed as k8s job, triggered on video upload
- RTL layout support (Arabic, Hebrew); i18n string extraction for all UI surfaces

---

### Phase 4 — Innovation (Sprint 24+)

**Goal:** deliver market-differentiating features that establish the platform as a next-generation LMS.

- **Virtual lab service:** Kubernetes ephemeral pod provisioner, browser-based VS Code (code-server), automated lab validation scripts, TTL enforcement
- **Blockchain credentials:** Open Badges 3.0 JWT issuance, optional on-chain anchoring (Polygon), LinkedIn auto-share, public `/verify/{hash}` endpoint
- **Engagement / wellbeing AI:** frustration/boredom signal detection from interaction patterns; automatic difficulty-pace adjustment; instructor weekly digest
- **AI translation service:** GPT-4 batch translation of lesson text + auto-subtitle generation; instructor review before publishing multilingual variant

---

## 5. Team Structure & Velocity

### 5.1 Team composition

| Role | FTEs | Responsibilities |
|---|---|---|
| Lead Architect / Dev | 1 | System design, Aspire AppHost, gateway, code review |
| Backend Engineers | 3 | Microservices, event bus, domain logic, APIs |
| AI / ML Engineers | 2 | RAG, recommendation, adaptive assessment, LLM grading |
| Full-stack Engineers | 2 | BFF layers, instructor UI, learner dashboard |
| DevOps / Platform | 1 | k8s, CI/CD, Keycloak, infra-as-code, monitoring |
| QA Engineer | 1 | Test automation, integration tests, load testing |
| Product / UX | 1 | Specs, wireframes, acceptance criteria, user testing |

**Total: 11 FTEs**

### 5.2 Velocity assumptions

- Sprint velocity: 40 story points per backend engineer per 2-week sprint
- Work packages estimated at 1–3 sprints of single-engineer effort
- 3 backend engineers allow parallel execution; Phase 1 calendar time reduced from 24 to 8 weeks
- AI team operates on separate track; spikes allowed for model evaluation in sprints 7–8
- 20% buffer built into each phase for QA, bug fixing, and sprint review feedback

---

## 6. Risk Register

| ID | Risk | Impact | Probability | Mitigation strategy |
|---|---|---|---|---|
| R1 | Keycloak realm misconfiguration | High | Medium | Test auth flows in CI; pre-import realm JSON |
| R2 | AI service latency / cost overrun | High | High | Cache embeddings; use async endpoints; set budget alerts |
| R3 | Multi-tenant data leakage | Critical | Low | EF Core global filters + integration test suite per tenant |
| R4 | Video transcoding bottleneck | Medium | Medium | Async queue; auto-scale transcoder pods; CDN pre-warm |
| R5 | Scope creep (feature additions) | High | High | Feature flags gate all Tier 3/4 work; strict sprint review |
| R6 | Plagiarism provider API downtime | Low | Medium | Fallback to internal pgvector similarity only; retry queue |
| R7 | k8s lab pod resource exhaustion | Medium | Low | TTL enforcement; resource quotas per namespace |
| R8 | GDPR deletion breaking audit trail | High | Low | Soft delete + anonymisation; never hard-delete audit log |

---

## 7. Definition of Done

Every work package must satisfy **all** criteria below before it is considered complete.

### 7.1 Code quality

- All unit tests pass with minimum 80% line coverage
- Integration tests cover the happy path and at least 2 error scenarios
- No Sonar critical/blocker findings outstanding
- Code reviewed and approved by at least one senior engineer

### 7.2 Observability

- Service emits structured logs with `CorrelationId` and `TenantId` on every request
- OpenTelemetry traces visible in Aspire Dashboard for all HTTP calls and DB queries
- Health check endpoint returns 200 with dependency status
- Alert rule configured in Prometheus for p99 latency > 500ms and error rate > 1%

### 7.3 Security

- JWT validation via Keycloak JWKS confirmed with negative test (expired/tampered token → 401)
- All secrets injected via environment variable / Azure Key Vault — no hardcoded credentials
- RBAC role enforcement tested: unauthenticated and wrong-role requests return 403

### 7.4 Infrastructure

- Service deployable via `aspire publish` → Docker Compose (local) and ACA/AKS (staging)
- Database migrations run automatically on startup; rollback script exists
- Feature flag gate implemented for any Tier 3/4 capability

### 7.5 Documentation

- OpenAPI spec generated and published to developer portal
- ADR (Architecture Decision Record) written for any significant design choice
- Runbook entry updated with new service's common failure scenarios and recovery steps

---

## 8. Success Metrics & KPIs

| Metric | Phase 1 target | Phase 2 target | Phase 3 target | Measurement |
|---|---|---|---|---|
| Course completion rate | — | > 65% | > 75% (AI-assisted paths) | Analytics worker projection |
| API p99 latency | < 300ms | < 300ms | < 300ms | Prometheus p99 histogram |
| Daily active learners | Baseline established | 20% WoW growth | Stable retention > 60% | AnalyticsWorker DAU query |
| Streak engagement rate | — | > 40% learners active streak | > 50% | GamificationService events |
| AI tutor satisfaction | — | CSAT > 4.0/5.0 | CSAT > 4.3/5.0 | Post-session rating widget |
| System uptime | 99.5% | 99.9% | 99.95% | Prometheus uptime monitor |

---

## Appendix: Microservice inventory

All services registered in `LMS.AppHost` via `.WithReference()`:

| Service | Database | Bus role | Key dependencies |
|---|---|---|---|
| `LMS.Gateway` (YARP) | Redis (session cache) | — | Keycloak JWKS |
| `LMS.IdentityService` | PostgreSQL | Publisher | Keycloak |
| `LMS.CourseService` | PostgreSQL | Publisher | — |
| `LMS.ContentService` | MongoDB + S3 | Publisher | CDN, Transcoder |
| `LMS.EnrollmentService` | PostgreSQL | Publisher/Consumer | PaymentService |
| `LMS.ProgressService` | PostgreSQL | Publisher/Consumer | xAPI LRS |
| `LMS.AssessmentService` | PostgreSQL + Redis | Publisher | pgvector |
| `LMS.GamificationService` | PostgreSQL + Redis | Consumer | SignalR |
| `LMS.LiveSessionService` | PostgreSQL | Publisher | Zoom/Meet API |
| `LMS.RecommendationService` | PostgreSQL + pgvector | Consumer | Semantic Kernel |
| `LMS.AiTutorService` | pgvector | Consumer | Semantic Kernel, LLM API |
| `LMS.PlagiarismWorker` | PostgreSQL + pgvector | Consumer | Copyleaks API |
| `LMS.AnalyticsWorker` | ClickHouse | Consumer | — |
| `LMS.NotificationWorker` | — | Consumer | SendGrid, FCM, Twilio |
| `LMS.PaymentService` | PostgreSQL | Publisher/Consumer | Stripe Connect |
| `LMS.MarketplaceService` | PostgreSQL | Publisher | Elasticsearch |
| `LMS.SkillsService` | PostgreSQL | Publisher/Consumer | — |
| `LMS.CertificateService` | PostgreSQL + S3 | Consumer | Open Badges 3.0 |
| `LMS.ComplianceService` | EventStoreDB | Consumer | S3 |
| `LMS.VirtualLabService` | PostgreSQL | Publisher | Kubernetes API |

## Appendix: Event contract reference

All events defined in `LMS.Contracts` (shared NuGet package):

| Event | Publisher | Consumers |
|---|---|---|
| `LessonCompleted` | ProgressService | Recommendation, Gamification, Analytics |
| `CourseCompleted` | ProgressService | Certificate, Gamification, Recommendation |
| `AssessmentCompleted` | AssessmentService | Recommendation, Gamification, Plagiarism |
| `SubmissionReceived` | AssessmentService | Plagiarism |
| `LiveSessionAttended` | LiveSessionService | Gamification, Progress, Analytics |
| `AchievementUnlocked` | GamificationService | Notification, Analytics |
| `EnrollmentCreated` | EnrollmentService | Progress, Notification, Analytics |
| `PaymentProcessed` | PaymentService | Enrollment, Notification |
| `UserEnrolled` | EnrollmentService | Progress, Gamification, Notification |
| `StreakBroken` | GamificationService | Notification |
| `LearnerAtRisk` | AnalyticsService | Notification (instructor alert) |
| `CoursePublished` | CourseService | Notification, Marketplace |
