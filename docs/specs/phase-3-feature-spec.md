# LMS Platform — Phase 3 Feature Specification

**Phase:** 3 — Enterprise & Scale  
**Sprints:** 17–23 (14 weeks)  
**Goal:** Unlock B2B and enterprise sales. Multi-tenancy, marketplace, compliance, advanced analytics, and peer learning make the platform enterprise-grade and commercially scalable.  
**Prerequisite:** All Phase 1 and Phase 2 work packages are complete and stable in production.  
**Stack:** Adds — Elasticsearch · Stripe Connect · Neo4j (optional) · EventStoreDB · cert-manager · Whisper · ML.NET · Workbox (PWA)

---

## Table of Contents

1. [WP 7.2 — Marketplace Service](#wp-72--marketplace-service)
2. [WP 7.3 — Revenue Sharing](#wp-73--revenue-sharing)
3. [WP 7.4 — Coupon & Promotions Engine](#wp-74--coupon--promotions-engine)
4. [WP 7.6 — Skills & Competency Framework](#wp-76--skills--competency-framework)
5. [WP 6.4 — Peer Review System](#wp-64--peer-review-system)
6. [WP 6.5 — Study Groups & Cohorts](#wp-65--study-groups--cohorts)
7. [WP 6.6 — Mentor Pairing](#wp-66--mentor-pairing)
8. [WP 4.3 — AI Course Generator](#wp-43--ai-course-generator)
9. [WP 4.5 — AI-assisted Content Authoring](#wp-45--ai-assisted-content-authoring)
10. [WP 4.6 — Auto Caption (Whisper)](#wp-46--auto-caption-whisper)
11. [WP 8.3 — Predictive At-risk Detection](#wp-83--predictive-at-risk-detection)
12. [WP 8.4 — Org-level BI & Data Export](#wp-84--org-level-bi--data-export)
13. [WP 8.5 — Skill Gap Analysis Dashboard](#wp-85--skill-gap-analysis-dashboard)
14. [WP 9.1 — GDPR Service](#wp-91--gdpr-service)
15. [WP 9.2 — Audit Log Service](#wp-92--audit-log-service)
16. [WP 9.4 — Content DRM & Security](#wp-94--content-drm--security)
17. [WP 9.5 — White-label & Multi-tenant](#wp-95--white-label--multi-tenant)
18. [WP 9.6 — Mandatory Training Tracking](#wp-96--mandatory-training-tracking)
19. [WP 10.1 — Offline / PWA Mode](#wp-101--offline--pwa-mode)
20. [WP 10.4 — Accessibility & i18n](#wp-104--accessibility--i18n)
21. [Open Design Decisions](#open-design-decisions)
22. [Phase 3 Event Contract Summary](#phase-3-event-contract-summary)
23. [Phase 3 API Surface Summary](#phase-3-api-surface-summary)

---

## WP 7.2 — Marketplace Service

**Sprint:** 17–18 | **Owner:** Full-stack | **Effort:** 2.5w  
**Service:** `LMS.MarketplaceService`

### Context

The Marketplace transforms the LMS from a closed system into an open course store. Instructors publish courses with pricing; students browse, search, and purchase. An admin approval workflow prevents low-quality content from reaching the marketplace. Search is powered by Elasticsearch for full-text and faceted filtering.

### User stories

**US-7.2.1** — As an instructor, I can list my course on the marketplace with a price, promotional description, and preview video so that potential students can discover it.

**US-7.2.2** — As a student, I can search the marketplace by keyword, category, difficulty, language, rating, and price range so that I find exactly what I need.

**US-7.2.3** — As a student, I can read reviews and ratings left by other students before purchasing so that I make an informed decision.

**US-7.2.4** — As a student, I can leave a rating and written review after completing at least 50% of a course so that I help other learners.

**US-7.2.5** — As an admin, I can approve or reject a course submitted to the marketplace with a rejection reason so that quality is maintained.

**US-7.2.6** — As a student, I can see a free preview of the first lesson before purchasing so that I know if the teaching style suits me.

**US-7.2.7** — As a student, I can add courses to a wishlist so that I can purchase them later.

### Acceptance criteria

**Instructor submission:**

- [ ] `POST /api/marketplace/listings` — instructor submits course for marketplace: `{ courseId, price, currency, previewLessonId, marketingDescription, targetAudience, whatYouWillLearn[] }`; status = `PendingApproval`
- [ ] `GET /api/marketplace/listings/mine` — returns instructor's listings with status and sales stats
- [ ] `PUT /api/marketplace/listings/{id}` — update listing details (re-triggers approval if price changes)
- [ ] `DELETE /api/marketplace/listings/{id}` — delist course (existing purchasers retain access)

**Admin approval:**

- [ ] `GET /api/marketplace/listings/pending` — admin only; returns all listings awaiting approval
- [ ] `POST /api/marketplace/listings/{id}/approve` — publishes listing; `CoursePublished` event emitted; indexed in Elasticsearch
- [ ] `POST /api/marketplace/listings/{id}/reject` — `{ reason }`; instructor notified via NotificationWorker

**Student discovery:**

- [ ] `GET /api/marketplace/search` — query params: `q`, `category`, `difficulty`, `language`, `minPrice`, `maxPrice`, `minRating`, `sort(relevance|newest|popular|rating)`; returns paginated results from Elasticsearch
- [ ] `GET /api/marketplace/listings/{id}` — full listing detail: description, syllabus summary, instructor profile, rating, review count, enrollment count, preview lesson URL
- [ ] `GET /api/marketplace/listings/{id}/preview` — returns signed URL for preview lesson (no enrollment required)
- [ ] `GET /api/marketplace/categories` — returns category tree with course counts

**Reviews:**

- [ ] `POST /api/marketplace/listings/{id}/reviews` — student posts review: `{ rating(1-5), title, body }`; requires ≥ 50% course completion; one review per student per course
- [ ] `GET /api/marketplace/listings/{id}/reviews` — returns paginated reviews sorted by `helpful` (upvotes), `newest`, `critical`
- [ ] `POST /api/marketplace/reviews/{id}/helpful` — toggle helpful vote; one vote per user
- [ ] `DELETE /api/marketplace/reviews/{id}` — author can delete own review; admin can delete any

**Wishlist:**

- [ ] `POST /api/marketplace/wishlist/{listingId}` — add to wishlist
- [ ] `DELETE /api/marketplace/wishlist/{listingId}` — remove from wishlist
- [ ] `GET /api/marketplace/wishlist` — returns student's wishlist with current prices

**Search index:**

- [ ] Elasticsearch index `lms_marketplace` with fields: `title`, `description`, `category`, `tags`, `difficulty`, `language`, `instructorName`, `rating`, `enrollmentCount`, `price`, `publishedAt`
- [ ] Index updated on: listing approved, listing updated, new review (updates avg rating)
- [ ] Autocomplete: `GET /api/marketplace/search/suggest?q=` — returns top 5 title suggestions

### Key data model

```
MarketplaceListing { Id, CourseId, TenantId, InstructorId, Price, Currency, MarketingDescription, TargetAudience, WhatYouWillLearn[], PreviewLessonId, Status(Draft/PendingApproval/Published/Delisted), ApprovedAt, ApprovedBy, CreatedAt }
CourseReview { Id, ListingId, CourseId, UserId, TenantId, Rating, Title, Body, HelpfulCount, CreatedAt, UpdatedAt }
Wishlist { UserId, TenantId, ListingId, AddedAt }
```

---

## WP 7.3 — Revenue Sharing

**Sprint:** 17–18 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.PaymentService` (extension) + `LMS.RevenueWorker`

### Context

When a student purchases a course on the marketplace, the revenue is split between the instructor and the platform. Instructors are paid out via Stripe Connect. A nightly worker calculates and initiates payouts.

### User stories

**US-7.3.1** — As an instructor, I can connect my Stripe account to the platform so that I receive my share of course sales.

**US-7.3.2** — As an instructor, I can see my earnings, pending payouts, and payout history on a dashboard so that I can track my income.

**US-7.3.3** — As an admin, I can configure the revenue split percentage (platform vs instructor) per course or globally so that business terms are flexible.

**US-7.3.4** — As a system, instructor payouts are processed automatically each week so that instructors are paid without manual intervention.

### Acceptance criteria

- [ ] `POST /api/payments/connect/onboard` — instructor initiates Stripe Connect onboarding; returns Stripe-hosted onboarding URL
- [ ] `GET /api/payments/connect/status` — returns `{ connected: bool, payoutsEnabled: bool, pendingRequirements[] }`
- [ ] `GET /api/payments/earnings` — instructor only; returns `{ totalEarned, pendingPayout, lastPayoutAmount, lastPayoutDate, earningsByMonth[] }`
- [ ] `GET /api/payments/earnings/transactions` — paginated list of per-sale earning records
- [ ] Revenue split config: global default 70% instructor / 30% platform; overridable per listing
- [ ] `PUT /api/marketplace/listings/{id}/revenue-split` — admin only; sets custom `instructorPercent` for a listing
- [ ] `RevenueWorker`: nightly job (2am UTC):
  1. Queries all `PaymentProcessed` events from past 7 days not yet paid out
  2. Groups by instructor Stripe Connect account
  3. Calculates instructor share per sale
  4. Creates Stripe Transfer to each connected account
  5. Records `PayoutRecord { InstructorId, Amount, Currency, StripeTransferId, PaidAt }`
- [ ] Minimum payout threshold: $25 (configurable); amounts below threshold roll over to next week
- [ ] `EarningTransaction { Id, InstructorId, PurchaseId, GrossAmount, PlatformFee, InstructorShare, Currency, PayoutId?, CreatedAt }`
- [ ] Refunds: if purchase refunded within 14 days, clawback instructor share via Stripe Transfer reversal

---

## WP 7.4 — Coupon & Promotions Engine

**Sprint:** 17–18 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.PaymentService` (extension)

### Context

Coupons allow instructors and admins to offer discounts. They support percentage and fixed-amount discount types, expiry dates, usage limits, and course-specific or platform-wide scope.

### User stories

**US-7.4.1** — As an instructor, I can create a coupon code for my course with a discount percentage and expiry date so that I can run promotions.

**US-7.4.2** — As a student, I can apply a coupon code at checkout to receive a discount so that I pay a reduced price.

**US-7.4.3** — As an admin, I can create platform-wide coupon codes for flash sales so that I can drive enrollment spikes.

**US-7.4.4** — As an instructor, I can see how many times a coupon has been used so that I can measure its effectiveness.

### Acceptance criteria

- [ ] `POST /api/payments/coupons` — create coupon: `{ code, discountType(Percent|Fixed), discountValue, courseId?, maxUses?, expiresAt, minPurchaseAmount? }`
- [ ] `GET /api/payments/coupons` — instructor sees own coupons; admin sees all; includes `usedCount`, `remainingUses`
- [ ] `DELETE /api/payments/coupons/{id}` — deactivate coupon (soft delete; existing uses unaffected)
- [ ] `POST /api/payments/coupons/validate` — student validates before checkout: `{ code, courseId }` → returns `{ valid, discountType, discountValue, finalPrice }`
- [ ] Coupon applied at Stripe Checkout session creation via `discounts` parameter
- [ ] Coupon enforcement: check `expiresAt`, `maxUses`, `courseId` match, `minPurchaseAmount` server-side — never trust client
- [ ] `CouponUsage { Id, CouponId, UserId, PurchaseId, DiscountApplied, UsedAt }` logged on every redemption
- [ ] Bundle pricing: `POST /api/payments/bundles` — admin creates bundle: `{ name, listingIds[], bundlePrice }`; student purchases all courses at bundle price
- [ ] Flash sale: coupon with `maxUses` and short `expiresAt`; admin can `POST /api/payments/flash-sales` as a convenience wrapper

### Key data model

```
Coupon { Id, TenantId, CreatedBy, Code, DiscountType, DiscountValue, CourseId?, MaxUses, UsedCount, MinPurchaseAmount, ExpiresAt, IsActive, CreatedAt }
Bundle { Id, TenantId, Name, ListingIds[], BundlePrice, Currency, IsActive }
```

---

## WP 7.6 — Skills & Competency Framework

**Sprint:** 17–18 | **Owner:** Backend | **Effort:** 2.5w  
**Service:** `LMS.SkillsService`

### Context

The SkillsService defines a taxonomy of skills (optionally SFIA-compatible) and maps courses and lessons to those skills with proficiency levels. Learners accumulate a skill passport. Organisations use the skill map to identify workforce gaps.

### User stories

**US-7.6.1** — As an admin, I can define a skill taxonomy with categories, skills, and proficiency levels so that the organisation has a shared vocabulary for competencies.

**US-7.6.2** — As an instructor, I can tag my course and individual lessons with skills and target proficiency levels so that the LMS knows what learners gain from my content.

**US-7.6.3** — As a student, I can see my skill passport — a profile of skills I have demonstrated and at what proficiency level — so that I can showcase my competencies.

**US-7.6.4** — As a student, I can export my skill passport as a JSON-LD credential to share on LinkedIn so that my learning is portable.

**US-7.6.5** — As an org admin, I can see a skill coverage map of my organisation — which skills are strong and which are gaps — so that I can plan learning interventions.

**US-7.6.6** — As a student, the learning path recommendation takes my skill gaps into account so that suggested courses address my actual weaknesses.

### Acceptance criteria

**Taxonomy management:**

- [ ] `POST /api/skills/taxonomy` — admin creates skill: `{ name, description, categoryId, sfiaCode?, proficiencyLevels[]{level(1-7), descriptor} }`
- [ ] `GET /api/skills/taxonomy` — returns full skill tree with categories and skills; public (no auth)
- [ ] `POST /api/skills/categories` — admin creates skill category (e.g. "Data Engineering", "Leadership")
- [ ] `PUT /api/skills/{id}` — admin updates skill definition
- [ ] Import SFIA 9 taxonomy from JSON seed file on first run (optional, configurable)

**Course/lesson skill tagging:**

- [ ] `POST /api/skills/mappings` — instructor tags content: `{ contentId, contentType(Course|Lesson), skillId, targetProficiencyLevel(1-7), weight(0.0-1.0) }`
- [ ] `GET /api/skills/mappings?contentId={id}` — returns skills associated with a piece of content
- [ ] `DELETE /api/skills/mappings/{id}` — instructor removes tag

**Learner skill passport:**

- [ ] `GET /api/skills/passport/me` — returns `{ skills: [{ skillId, name, demonstratedLevel, evidence[]{courseId, completedAt, score} }] }`
- [ ] Skill passport updated automatically on `CourseCompleted`: all skills tagged to that course are credited at `targetProficiencyLevel * completionPercent`
- [ ] `GET /api/skills/passport/{userId}` — org admin can view any user's passport within their tenant
- [ ] `GET /api/skills/passport/me/export` — returns JSON-LD Open Skills credential; downloadable
- [ ] LinkedIn share URL generated alongside export

**Org skill gap analysis:**

- [ ] `GET /api/skills/org/coverage` — org admin; returns per-skill: `{ skillId, name, avgProficiency, learnersWithSkill, learnersBelow(level), topGapCourses[] }`
- [ ] `GET /api/skills/org/gaps?minLevel={n}` — returns skills where org average proficiency is below `minLevel`; sorted by severity
- [ ] RecommendationService integration: `GET /api/skills/gaps/me` — returns personal skill gaps used by WP 4.1

### Key data model

```
SkillCategory { Id, TenantId?, Name, Description, ParentCategoryId? }
Skill { Id, TenantId?, CategoryId, Name, Description, SfiaCode?, ProficiencyLevels[]{Level, Descriptor} }
ContentSkillMapping { Id, ContentId, ContentType, SkillId, TenantId, TargetProficiencyLevel, Weight }
LearnerSkillRecord { Id, UserId, SkillId, TenantId, DemonstratedLevel, LastUpdatedAt }
SkillEvidence { Id, LearnerSkillRecordId, CourseId, CompletedAt, Score, ContributedLevel }
```

---

## WP 6.4 — Peer Review System

**Sprint:** 19–20 | **Owner:** Full-stack | **Effort:** 2w  
**Service:** `LMS.PeerReviewService`

### Context

Peer review allows students to assess each other's assignments using a structured rubric. This scales assessment for large cohorts without requiring instructor time for every submission. Blind review (reviewer doesn't see submitter's name) is supported.

### User stories

**US-6.4.1** — As an instructor, I can create a peer review assignment with a rubric so that students assess each other's work.

**US-6.4.2** — As an instructor, I can configure whether reviews are blind (anonymous) or open so that I control the dynamics of the review process.

**US-6.4.3** — As a student, I submit my assignment and then review a set number of my peers' submissions using the rubric so that I learn through both submitting and reviewing.

**US-6.4.4** — As a student, I receive aggregated feedback from my reviewers so that I understand multiple perspectives on my work.

**US-6.4.5** — As an instructor, I can see all submissions and their aggregated peer scores so that I can moderate outlier scores.

**US-6.4.6** — As a student, I must complete my assigned peer reviews before I can see my own feedback so that I engage fully with the review process.

### Acceptance criteria

**Assignment creation (instructor):**

- [ ] `POST /api/peer-reviews/assignments` — `{ courseId, lessonId?, title, instructions, rubric[]{criterionName, description, maxPoints}, reviewsPerSubmission(default 3), isBlind(default true), submissionDeadline, reviewDeadline }`
- [ ] `GET /api/peer-reviews/assignments?courseId={id}` — returns assignments for a course with submission/review counts
- [ ] `PUT /api/peer-reviews/assignments/{id}` — update (only before submission deadline)

**Student submission:**

- [ ] `POST /api/peer-reviews/assignments/{id}/submissions` — student submits: `{ body(markdown, max 5000 chars) | attachmentContentId }`; one submission per student per assignment
- [ ] `GET /api/peer-reviews/assignments/{id}/submissions/me` — returns own submission with status and received reviews (only after review deadline)

**Review assignment & submission:**

- [ ] On submission deadline: system auto-assigns reviewers using round-robin algorithm ensuring no student reviews their own submission; each submission gets exactly `reviewsPerSubmission` reviewers
- [ ] `GET /api/peer-reviews/reviews/assigned` — returns submissions assigned to current student for review (blinded if `isBlind = true`)
- [ ] `POST /api/peer-reviews/reviews` — student submits review: `{ submissionId, scores[]{criterionId, score}, overallFeedback }`; one review per assigned submission
- [ ] Student cannot view own feedback until they have completed all assigned reviews
- [ ] After `reviewDeadline`: system calculates aggregate score per submission: `avg(reviewerScores)` with outlier detection (scores > 2σ from mean discarded)

**Instructor oversight:**

- [ ] `GET /api/peer-reviews/assignments/{id}/submissions` — instructor sees all submissions with aggregate peer scores
- [ ] `POST /api/peer-reviews/submissions/{id}/override` — instructor overrides aggregate score with their own: `{ finalScore, comment }`
- [ ] `GET /api/peer-reviews/assignments/{id}/outliers` — returns submissions where reviewer scores had high variance
- [ ] `AssessmentSubmitted` event published when peer review score is finalised (for GamificationService and ProgressService)

### Key data model

```
PeerReviewAssignment { Id, CourseId, LessonId?, TenantId, InstructorId, Title, Instructions, Rubric[]{CriterionId, Name, Description, MaxPoints}, ReviewsPerSubmission, IsBlind, SubmissionDeadline, ReviewDeadline }
PeerSubmission { Id, AssignmentId, UserId, TenantId, Body, AttachmentContentId?, AggregateScore?, InstructorOverrideScore?, SubmittedAt }
PeerReview { Id, SubmissionId, ReviewerId, TenantId, Scores[]{CriterionId, Score}, OverallFeedback, IsOutlier, SubmittedAt }
ReviewAssignment { SubmissionId, ReviewerId, TenantId, CompletedAt? }
```

---

## WP 6.5 — Study Groups & Cohorts

**Sprint:** 19–20 | **Owner:** Full-stack | **Effort:** 1.5w  
**Service:** `LMS.CohortService`

### Context

Study groups are learner-created sub-communities within a course. They have a capped membership, shared goals, and a group leaderboard. Cohorts are instructor-created groups (e.g., "September 2026 intake") that segment students for analytics and communication.

### User stories

**US-6.5.1** — As a student, I can create a study group for a course and invite peers so that we can learn together.

**US-6.5.2** — As a student in a study group, I can see a group leaderboard and shared progress so that we stay motivated together.

**US-6.5.3** — As a student, I can see group study goals and whether the group is on track to meet them so that I feel collective accountability.

**US-6.5.4** — As an instructor, I can create a cohort to segment students (e.g., by intake date or department) so that I can target communications and analytics.

**US-6.5.5** — As an instructor, I can send a message to all students in a cohort so that I communicate efficiently with targeted groups.

### Acceptance criteria

**Study groups (learner-created):**

- [ ] `POST /api/cohorts/groups` — student creates group: `{ courseId, name, description, maxMembers(2-10) }`; creator becomes group admin
- [ ] `POST /api/cohorts/groups/{id}/invite` — group admin invites by userId; invitee receives notification
- [ ] `POST /api/cohorts/groups/{id}/join` — student joins via invite link or open group; fails if `maxMembers` reached
- [ ] `DELETE /api/cohorts/groups/{id}/members/{userId}` — group admin removes member or member leaves
- [ ] `GET /api/cohorts/groups/{id}/leaderboard` — returns members ranked by XP within the course scope
- [ ] `GET /api/cohorts/groups/{id}/progress` — returns per-member completion % and last active date
- [ ] `POST /api/cohorts/groups/{id}/goals` — group admin sets shared group goal: `{ targetType, targetValue, deadline }` (same model as WP 5.4)
- [ ] `GET /api/cohorts/groups/{id}/goals` — returns group goals with aggregate progress

**Cohorts (instructor-created):**

- [ ] `POST /api/cohorts` — instructor creates cohort: `{ courseId, name, description, memberUserIds[]? }`
- [ ] `POST /api/cohorts/{id}/members` — add students to cohort
- [ ] `DELETE /api/cohorts/{id}/members/{userId}` — remove student from cohort
- [ ] `GET /api/cohorts/{id}/analytics` — returns cohort-level completion %, avg score, active this week (sourced from AnalyticsWorker)
- [ ] `POST /api/cohorts/{id}/announce` — instructor sends announcement to all cohort members via NotificationWorker

### Key data model

```
StudyGroup { Id, CourseId, TenantId, Name, Description, MaxMembers, CreatedBy, CreatedAt }
GroupMember { GroupId, UserId, TenantId, Role(Admin|Member), JoinedAt }
Cohort { Id, CourseId, TenantId, Name, Description, CreatedBy, CreatedAt }
CohortMember { CohortId, UserId, TenantId, AddedAt }
```

---

## WP 6.6 — Mentor Pairing

**Sprint:** 19–20 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.CohortService` (extension)

### Context

Mentor pairing matches advanced students (high mastery score, completed course) with beginners (low mastery, just enrolled) within the same course. Mentors volunteer; the algorithm matches based on skill complementarity.

### User stories

**US-6.6.1** — As an advanced student, I can volunteer to be a mentor for a course so that I can give back to the community.

**US-6.6.2** — As a new student, I can request a mentor for a course so that I have a guide to help me through difficult sections.

**US-6.6.3** — As a matched pair, we can exchange messages within the platform so that we can communicate without sharing personal contact details.

**US-6.6.4** — As a student who received mentorship, I can leave feedback on my mentor so that good mentors are recognised.

### Acceptance criteria

- [ ] `POST /api/mentoring/volunteer` — student opts in as mentor: `{ courseId, weeklyHoursAvailable, bio }`; requires course completion + avg score ≥ 80%
- [ ] `POST /api/mentoring/request` — student requests a mentor: `{ courseId, strugglingWith }`
- [ ] Matching algorithm (runs daily): for each unmatched request, find available mentor with: highest mastery score delta to mentee, fewest current mentees (cap: 3 per mentor), same tenant
- [ ] `GET /api/mentoring/matches/me` — returns active mentor/mentee relationships with contact thread ID
- [ ] `POST /api/mentoring/messages` — send message in a mentoring thread: `{ matchId, content(markdown) }`; stored in `MentoringMessage` table; real-time delivery via SignalR if both online
- [ ] `GET /api/mentoring/messages/{matchId}` — returns conversation history
- [ ] `POST /api/mentoring/matches/{id}/feedback` — mentee rates mentor: `{ rating(1-5), comment }` after match closes
- [ ] `POST /api/mentoring/matches/{id}/close` — either party can close a match with a reason
- [ ] `MentorRating` aggregated per mentor; displayed on volunteer profile
- [ ] `HelpfulMentor` badge awarded when mentor receives avg rating ≥ 4.5 across 3+ completed matches

### Key data model

```
MentorProfile { UserId, CourseId, TenantId, WeeklyHoursAvailable, Bio, IsActive, AvgRating, MatchCount }
MentorMatch { Id, MentorId, MenteeId, CourseId, TenantId, Status(Active|Closed), MatchedAt, ClosedAt, ClosureReason? }
MentoringMessage { Id, MatchId, SenderId, TenantId, Content, SentAt }
MentorFeedback { MatchId, Rating, Comment, SubmittedAt }
```

---

## WP 4.3 — AI Course Generator

**Sprint:** 19–20 | **Owner:** AI Team | **Effort:** 3w  
**Service:** `LMS.AiCourseGeneratorService`

### Context

An instructor provides a topic, target audience, learning objectives, and desired duration. The AI generates a complete course outline, lesson content drafts, and quiz questions. The instructor reviews and edits everything before publishing — the AI is a drafting assistant, not a publisher.

### User stories

**US-4.3.1** — As an instructor, I can describe a course I want to create in plain language and the AI produces a full draft syllabus so that I save days of content planning.

**US-4.3.2** — As an instructor, I can regenerate any individual lesson or section if the AI draft doesn't match my vision so that I stay in creative control.

**US-4.3.3** — As an instructor, the AI generates quiz questions appropriate for each lesson so that I have an assessment starting point.

**US-4.3.4** — As an instructor, all AI-generated content is clearly labelled as a draft requiring my review before it can be published so that students never see unreviewed AI content.

**US-4.3.5** — As an instructor, I can provide my existing slides or notes as input so that the AI generates content grounded in my own material.

### Acceptance criteria

- [ ] `POST /api/ai-generator/courses` — instructor submits generation request: `{ topic, targetAudience, learningObjectives[], durationHours, difficulty, language, instructorNotes?, uploadedDocumentContentIds[]? }`; returns `{ jobId }`; async processing
- [ ] `GET /api/ai-generator/courses/{jobId}` — poll job status: `Queued → Generating → Review → Failed`
- [ ] Generation pipeline:
  1. If documents uploaded: extract text via ContentService → include as context in LLM prompt
  2. Call LLM to generate: course outline (sections + lesson titles + learning objectives per lesson)
  3. For each lesson (parallel calls, max 5 concurrent): generate lesson body (markdown, ~800 words) + 3 MCQ quiz questions
  4. Store draft in `GeneratedCourse` with all content as `status = Draft`
- [ ] `GET /api/ai-generator/courses/{jobId}/result` — returns full generated course structure with all draft content
- [ ] `POST /api/ai-generator/courses/{jobId}/lessons/{lessonId}/regenerate` — regenerate a single lesson with optional `{ additionalInstructions }`
- [ ] `POST /api/ai-generator/courses/{jobId}/publish` — instructor approves and pushes draft to CourseService as a real course in `Draft` status (not yet published to students)
- [ ] Generated content always tagged `{ aiGenerated: true, reviewedAt: null }` until instructor explicitly approves each section
- [ ] `POST /api/ai-generator/lessons/{id}/approve` — instructor marks lesson as reviewed; removes AI draft label
- [ ] Instructor cannot call `CourseService publish` on AI-generated course until all lessons are approved
- [ ] Max generation job: 20 lessons; larger courses must be generated in batches
- [ ] Cost guardrail: max tokens per generation job logged and capped per tenant per day

### Key data model

```
GenerationJob { Id, InstructorId, TenantId, Status, Topic, TargetAudience, LearningObjectives[], DurationHours, Difficulty, Language, TokensUsed, CreatedAt, CompletedAt }
GeneratedLesson { Id, JobId, TenantId, Title, Body, LearningObjectives[], IsApproved, RegenerationCount, CreatedAt }
GeneratedQuestion { Id, GeneratedLessonId, TenantId, Prompt, Options[], CorrectOptionIndex, IsApproved }
```

---

## WP 4.5 — AI-assisted Content Authoring

**Sprint:** 19–20 | **Owner:** AI Team | **Effort:** 2w  
**Service:** `LMS.CourseService` (extension — authoring UI feature)

### Context

Inline AI assistance in the lesson editor helps instructors write better content faster. Unlike the AI Course Generator (which creates from scratch), this is a contextual writing assistant embedded in the block-based editor.

### User stories

**US-4.5.1** — As an instructor writing a lesson, I can highlight a section and ask the AI to expand, simplify, or improve it so that my writing quality is higher.

**US-4.5.2** — As an instructor, I can select a lesson's text and ask the AI to generate quiz questions from it so that creating assessments is fast.

**US-4.5.3** — As an instructor, I can ask the AI to suggest a real-world example for a concept I'm explaining so that my content is more relatable.

**US-4.5.4** — As an instructor, all AI suggestions appear inline as tracked changes that I explicitly accept or reject so that I maintain authorial control.

### Acceptance criteria

- [ ] `POST /api/authoring/ai/assist` — `{ lessonId, selectedText, action(Expand|Simplify|Improve|GenerateQuiz|SuggestExample), additionalContext? }` → returns `{ suggestion }`; streamed via SSE
- [ ] Actions:
  - `Expand`: generate 2–3 additional paragraphs continuing the selected text
  - `Simplify`: rewrite at one reading level lower (Flesch-Kincaid target: 60+)
  - `Improve`: grammar, clarity, and conciseness pass
  - `GenerateQuiz`: return 3 MCQ questions as JSON (compatible with AssessmentService question schema)
  - `SuggestExample`: return 1 concrete real-world example paragraph
- [ ] AI suggestion presented in UI as a "ghost text" overlay with Accept / Reject / Regenerate buttons
- [ ] Accepted suggestions are appended/replacing selected text in the lesson body; logged to `AiAuthoringLog`
- [ ] `AiAuthoringLog { LessonId, InstructorId, Action, InputTokens, OutputTokens, Accepted, CreatedAt }` — for cost tracking
- [ ] Rate limit: 20 AI assist calls per instructor per hour (configurable)
- [ ] No AI suggestion is ever auto-saved without explicit instructor acceptance

---

## WP 4.6 — Auto Caption (Whisper)

**Sprint:** 21 | **Owner:** AI Team | **Effort:** 1w  
**Service:** `LMS.ContentService` (extension)

### Context

When a video is uploaded and transcoded, a Whisper ASR (Automatic Speech Recognition) job runs in parallel to generate captions in the video's original language. Translated captions are added in Phase 4 (WP 10.5).

### User stories

**US-4.6.1** — As a student, every video lesson has auto-generated captions so that I can follow along in noisy environments or if I am hearing impaired.

**US-4.6.2** — As an instructor, I can review and edit auto-generated captions before they are published so that accuracy is correct.

**US-4.6.3** — As a student, I can search within a video by caption text so that I can jump to the exact moment a concept is discussed.

### Acceptance criteria

- [ ] On `ContentProcessingCompleted` (video ready): trigger Whisper transcription k8s Job
- [ ] Whisper model: `whisper-large-v3` deployed as containerised inference service; GPU-accelerated if available; CPU fallback
- [ ] Output: VTT caption file stored in S3 alongside HLS segments; linked to `ContentItem.CaptionVttUrl`
- [ ] `GET /api/content/{id}/captions` — returns signed URL for VTT file; player auto-loads captions
- [ ] `PUT /api/content/{id}/captions` — instructor uploads corrected VTT file (overwrites auto-generated)
- [ ] `GET /api/content/{id}/transcript` — returns full plain-text transcript; stored in MongoDB for search indexing
- [ ] Elasticsearch indexes caption text per `contentId` with timestamps → enables `GET /api/content/search?q=` across video transcripts
- [ ] Transcription job timeout: 60 minutes; alert if exceeded
- [ ] Caption language detected automatically via Whisper's language detection; stored in `ContentItem.CaptionLanguage`
- [ ] WCAG 2.1 AA: captions must cover ≥ 98% of spoken words (measured via word error rate check against instructor-corrected reference if available)

---

## WP 8.3 — Predictive At-risk Detection

**Sprint:** 21–22 | **Owner:** AI Team | **Effort:** 2.5w  
**Service:** `LMS.AnalyticsWorker` (extension)

### Context

The at-risk model identifies students likely to drop out before completing a course. It scores students daily using a combination of engagement signals and notifies instructors so they can intervene proactively.

### User stories

**US-8.3.1** — As an instructor, I receive an alert when a student in my course is predicted to drop out so that I can reach out before they disengage.

**US-8.3.2** — As an instructor, I can see which students are at risk and why (what signals drove the prediction) so that my outreach is targeted and relevant.

**US-8.3.3** — As an admin, I can see the platform-wide at-risk rate over time so that I can measure the impact of interventions.

### Acceptance criteria

- [ ] Daily batch job scores all active enrollments using the at-risk model
- [ ] Feature vector per student per course (computed from ClickHouse):
  - Days since last activity
  - Completion % vs expected % (based on enrollment date and course duration)
  - Quiz score trend (improving / stable / declining)
  - Forum participation count (last 14 days)
  - Live session attendance rate
  - Streak status (active / broken)
- [ ] Model: Gradient Boosted Decision Tree (ML.NET `FastTree`) trained on historical `CourseCompleted` vs `EnrollmentAbandoned` labels
- [ ] At-risk threshold: probability of dropout > 0.65 → flag as `AtRisk`
- [ ] `LearnerAtRisk` event published for each newly-flagged student → NotificationWorker sends instructor alert
- [ ] Alert contains: student name, course, last active date, top 3 risk factors (feature importance), suggested action ("Send encouragement message", "Assign remedial content", "Schedule a check-in")
- [ ] `GET /api/analytics/instructor/courses/{courseId}/at-risk` — returns list of at-risk students with risk score and factor breakdown
- [ ] `POST /api/analytics/at-risk/{enrollmentId}/dismiss` — instructor dismisses alert (student removed from at-risk list for 7 days)
- [ ] Model retrained weekly on new data; model version tracked; previous version retained for rollback
- [ ] `GET /api/analytics/platform/at-risk-rate` — admin; returns `{ atRiskCount, atRiskRate, weekOverWeek }` time series

### Key data model

```
AtRiskScore { Id, UserId, CourseId, TenantId, RiskScore, RiskFactors[]{factor, contribution}, ScoredAt, Status(Active|Dismissed|Resolved) }
ModelVersion { Id, TrainedAt, Accuracy, F1Score, IsActive }
```

---

## WP 8.4 — Org-level BI & Data Export

**Sprint:** 21–22 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.AnalyticsWorker` (extension)

### Context

Enterprise org admins need to pull LMS data into their own BI tools (Power BI, Tableau, Looker). This work package adds scheduled data exports to S3 and embeddable report iframes.

### User stories

**US-8.4.1** — As an org admin, I can schedule a weekly data export of all learning activity to an S3 bucket so that I can load it into our data warehouse.

**US-8.4.2** — As an org admin, I can embed LMS analytics reports in our internal portal using an iframe so that stakeholders see live data without needing LMS accounts.

**US-8.4.3** — As an org admin, I can download a CSV of all enrollment, completion, and assessment data for a date range so that I can do ad-hoc analysis.

### Acceptance criteria

- [ ] `POST /api/analytics/exports` — schedule export: `{ type(CSV|Parquet), tables[](enrollments|completions|assessments|payments), schedule(OneTime|Weekly|Monthly), s3Destination? }`
- [ ] `GET /api/analytics/exports` — returns scheduled exports and their last run status
- [ ] `GET /api/analytics/exports/{id}/download` — returns signed S3 URL for latest export file (TTL 1 hour)
- [ ] Export content: all data scoped to `TenantId`; no cross-tenant data ever in same export file
- [ ] CSV format: header row + one row per record; UTF-8; date fields in ISO 8601
- [ ] Parquet format: columnar; compatible with AWS Athena, Google BigQuery, Azure Synapse
- [ ] Embeddable reports: `GET /api/analytics/embed-token` — returns short-lived signed token (15 min TTL); used to render report iframe at `/reports/{type}?token={token}`
- [ ] Embedded reports available: `course_completions`, `enrollment_trends`, `skill_coverage`, `revenue_summary`
- [ ] GDPR compliance: export jobs log who requested what data and when in `AuditLog` (WP 9.2)
- [ ] Power BI connector: publish OData endpoint `GET /odata/v1/enrollments` (etc.) with read-only API key auth for Power BI direct query mode

---

## WP 8.5 — Skill Gap Analysis Dashboard

**Sprint:** 21 | **Owner:** Full-stack | **Effort:** 1w  
**Service:** `LMS.SkillsService` + `LMS.AnalyticsWorker`

### Context

The skill gap dashboard gives org admins a visual overview of skill coverage across their workforce, which skills are deficient, and which courses address those gaps. It is built on top of the SkillsService data from WP 7.6.

### User stories

**US-8.5.1** — As an org admin, I can see a heatmap of skills vs departments showing where proficiency is strong and where it is weak so that I prioritise training budgets.

**US-8.5.2** — As an org admin, I can click on a skill gap and see the recommended courses that address it so that I can assign training.

**US-8.5.3** — As an org admin, I can set a target proficiency level per skill and see how many learners are below it so that I measure compliance with competency standards.

### Acceptance criteria

- [ ] `GET /api/skills/org/gap-analysis` — returns `{ skills[]{skillId, name, targetLevel, avgActualLevel, belowTargetCount, belowTargetPercent, recommendedCourses[]{courseId, title, avgSkillGain} } }`
- [ ] `POST /api/skills/org/targets` — admin sets `{ skillId, targetLevel }` for the organisation
- [ ] `GET /api/skills/org/targets` — returns all skill targets with current attainment
- [ ] `POST /api/skills/org/bulk-assign` — admin assigns a course to all users who are below target for a skill: `{ skillId, courseId }`; calls EnrollmentService to create enrollments
- [ ] Dashboard renders in the frontend as: skill category tabs → skill rows → proficiency bar (actual vs target) → click → drill into course recommendations
- [ ] Data sourced from SkillsService `LearnerSkillRecord` aggregated by tenant; cached in Redis for 1 hour
- [ ] Export: `GET /api/skills/org/gap-analysis/export` → CSV with one row per skill per user

---

## WP 9.1 — GDPR Service

**Sprint:** 21–22 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.GdprService`

### Context

GDPR compliance requires two key capabilities: the right to erasure (a user can request all their personal data be deleted) and the right to data portability (a user can export all their data). Critically, erasure must not break audit logs — personal data is anonymised, not hard-deleted from immutable logs.

### User stories

**US-9.1.1** — As a student, I can submit a right-to-erasure request so that my personal data is removed from the platform.

**US-9.1.2** — As a student, I can export all data the platform holds about me as a JSON file so that I can see exactly what is stored.

**US-9.1.3** — As a system, erasure anonymises personal data across all services without breaking audit trail integrity so that compliance and immutability are both preserved.

**US-9.1.4** — As an admin, I can see all pending and completed data requests so that I can meet the 30-day GDPR response deadline.

### Acceptance criteria

- [ ] `POST /api/gdpr/erasure-requests` — student submits right-to-erasure; identity verified via re-authentication (Keycloak re-auth flow)
- [ ] `POST /api/gdpr/export-requests` — student requests data export; delivered within 72 hours
- [ ] `GET /api/gdpr/requests` — admin sees all requests with status and deadline (30-day SLA from submission)
- [ ] `GET /api/gdpr/export-requests/{id}/download` — returns signed S3 URL for data export ZIP

**Erasure pipeline (orchestrated by GdprService via bus commands):**

| Service | Erasure action |
|---|---|
| IdentityService | Delete profile; disable Keycloak account |
| CourseService | Remove instructor attribution (replace with "Deleted Instructor") |
| ContentService | Delete uploaded media; replace metadata author with "Anonymised" |
| ProgressService | Anonymise `UserId` to `GDPR_ERASED_{hash}` in all records |
| AssessmentService | Anonymise `UserId` in attempts; delete essay bodies |
| GamificationService | Delete leaderboard entry; anonymise XP transactions |
| ForumService | Replace post author with "Deleted User"; retain post body (public discourse) |
| PaymentService | Retain payment records (legal obligation 7 years); anonymise name/email |
| AuditLogService | Retain log entries; anonymise `UserId` field only |
| CertificateService | Revoke certificates; delete PDF from S3 |

- [ ] Each service exposes `POST /internal/gdpr/erase/{userId}` (internal, service-to-service only)
- [ ] GdprService orchestrates via Saga pattern: publishes `EraseUserData` commands per service; awaits `UserDataErased` confirmations; retries on timeout
- [ ] Erasure SLA: complete within 30 days of request (GDPR requirement)
- [ ] `ErasureRequest { Id, UserId, TenantId, Status(Pending/InProgress/Completed/Failed), SubmittedAt, CompletedAt, Steps[]{Service, Status, CompletedAt} }`
- [ ] Data export ZIP contains: profile JSON, enrolled courses, progress records, assessment attempts, certificates, payment history, forum posts, gamification history — all in human-readable JSON

---

## WP 9.2 — Audit Log Service

**Sprint:** 21–22 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.AuditLogService`

### Context

An immutable audit log captures every significant action taken on the platform. This is required for SOC 2, ISO 27001, and GDPR compliance. EventStoreDB (or Kafka with retention disabled) provides append-only semantics.

### User stories

**US-9.2.1** — As a compliance officer, I can query the audit log to see every action taken by a specific user so that I can investigate incidents.

**US-9.2.2** — As an admin, I can see when and by whom any course, enrollment, or grade was changed so that there is full accountability.

**US-9.2.3** — As a system, audit log entries can never be deleted or modified so that the record is tamper-proof.

**US-9.2.4** — As an admin, I can export audit logs for a date range to S3 as part of SOC 2 evidence collection.

### Acceptance criteria

- [ ] Every service publishes `AuditEvent` to the bus on significant actions (see table below)
- [ ] AuditLogService consumes all `AuditEvent` messages and appends to EventStoreDB stream `audit-{tenantId}`
- [ ] EventStoreDB configured with: no stream deletion, no event deletion, max stream age = never
- [ ] `GET /api/audit-log` — admin/compliance role; query params: `userId`, `action`, `resourceType`, `resourceId`, `from`, `to`; paginated; max 10,000 records per query
- [ ] `GET /api/audit-log/export` — returns S3 signed URL for NDJSON export of query results
- [ ] Each `AuditEvent` contains: `{ EventId, TenantId, ActorId, ActorRole, Action, ResourceType, ResourceId, Metadata{}, IpAddress, UserAgent, OccurredAt }`
- [ ] `IpAddress` sourced from gateway `X-Forwarded-For` header; passed as claim in JWT or request header
- [ ] Audit log query response time: < 2s for 90-day window queries

**Actions to audit (minimum set):**

| Action | Trigger |
|---|---|
| `User.Login` | Keycloak login event (via webhook) |
| `User.RoleChanged` | IdentityService role update |
| `User.Deactivated` | IdentityService deactivation |
| `Course.Published` | CourseService publish |
| `Course.Unpublished` | CourseService unpublish |
| `Enrollment.Created` | EnrollmentService |
| `Enrollment.Revoked` | EnrollmentService admin revoke |
| `Grade.Released` | AssessmentService grade release |
| `Grade.Overridden` | AssessmentService instructor override |
| `Certificate.Issued` | CertificateService |
| `Certificate.Revoked` | CertificateService |
| `Payment.Refunded` | PaymentService |
| `GdprErasure.Completed` | GdprService |
| `AuditLog.Exported` | AuditLogService self-log |

---

## WP 9.4 — Content DRM & Security

**Sprint:** 21–22 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.ContentService` (extension)

### Context

Paid content must be protected against sharing and redistribution. Phase 3 adds short-lived signed URL rotation, PDF watermarking with the learner's user ID, and device-limit enforcement.

### User stories

**US-9.4.1** — As a content owner, video stream URLs expire after 15 minutes so that enrolled students cannot share permanent links to paid content.

**US-9.4.2** — As a content owner, PDF downloads are watermarked with the student's name and user ID so that leaked PDFs can be traced.

**US-9.4.3** — As a content owner, a student can only stream content on up to 2 devices simultaneously so that account sharing is limited.

**US-9.4.4** — As an admin, I can restrict a course to students in specific countries so that I comply with regional licensing restrictions.

### Acceptance criteria

**Signed URLs:**

- [ ] All HLS manifest URLs: TTL = 15 minutes; signed with CloudFront key pair
- [ ] On TTL expiry, player requests a new signed URL via `GET /api/content/{id}/stream/refresh` (requires valid JWT); new URL issued if enrollment still active
- [ ] Signed URL contains: `userId`, `contentId`, `expiresAt` — tampering invalidates signature

**PDF watermarking:**

- [ ] `GET /api/content/{id}/download` — server-side adds invisible text watermark to PDF: "Licensed to {displayName} ({userId}) on {date}" embedded as transparent text layer
- [ ] Watermark generated on-the-fly using `iTextSharp` or `PdfPig`; watermarked PDF not stored (generated per request)
- [ ] Visible watermark option (configurable per course): adds semi-transparent diagonal text across each page

**Device limits:**

- [ ] On `GET /api/content/{id}/stream`: record `{ userId, contentId, deviceFingerprint, startedAt }` in Redis with TTL = 30 minutes
- [ ] `deviceFingerprint` = hash of `User-Agent + X-Forwarded-For + Accept-Language` (client-side component optional)
- [ ] If `activeStreams > 2` for `userId`: return `403 Too Many Devices`; student sees "You're already streaming on 2 devices"
- [ ] Device session refreshed every 5 minutes by player heartbeat `POST /api/content/{id}/stream/heartbeat`

**Geo-restriction:**

- [ ] `PUT /api/courses/{id}/geo-restrictions` — instructor/admin sets `{ allowedCountryCodes[] }` or `{ blockedCountryCodes[] }`
- [ ] Gateway checks `X-Country-Code` header (set by CDN/proxy from IP geolocation)
- [ ] If restricted: content endpoints return `451 Unavailable For Legal Reasons` with explanation

---

## WP 9.5 — White-label & Multi-tenant

**Sprint:** 21–22 | **Owner:** Full-stack | **Effort:** 3w  
**Service:** `LMS.TenantService` + infrastructure

### Context

Enterprise customers want the LMS to appear as their own branded product. This requires custom domains with auto-provisioned SSL, per-tenant theme configuration, and tenant-scoped Keycloak settings. The TenantService is a new service that manages all tenant configuration beyond what IdentityService already handles.

### User stories

**US-9.5.1** — As an org admin, I can configure a custom domain (e.g., `learn.mycompany.com`) so that the LMS appears as our own product.

**US-9.5.2** — As an org admin, I can upload my company logo, set brand colours, and choose a font so that the platform matches our visual identity.

**US-9.5.3** — As an org admin, I configure my company's SSO (SAML or OIDC) so that employees use our existing identity provider to log in.

**US-9.5.4** — As a system, each tenant's custom domain automatically gets a valid TLS certificate so that we don't manage SSL manually.

**US-9.5.5** — As a platform admin, I can create and configure a new tenant from an admin portal in under 5 minutes so that onboarding is fast.

### Acceptance criteria

**Custom domain:**

- [ ] `POST /api/tenants/{id}/domain` — org admin submits `{ customDomain: "learn.mycompany.com" }`
- [ ] System creates: DNS verification TXT record `_lms-verify.{customDomain}` with a unique token
- [ ] `POST /api/tenants/{id}/domain/verify` — system checks DNS; on success, provisions TLS cert via cert-manager (Let's Encrypt) + creates Kubernetes Ingress for custom domain
- [ ] YARP gateway: routes requests arriving on `learn.mycompany.com` to the correct tenant by mapping domain → `TenantId`
- [ ] Domain verified badge shown in tenant admin portal; status: `Pending / Verifying / Active / Failed`
- [ ] TLS cert auto-renewed 30 days before expiry via cert-manager

**Theming:**

- [ ] `PUT /api/tenants/{id}/theme` — `{ logoUrl, primaryColor(hex), secondaryColor(hex), fontFamily, faviconUrl }`
- [ ] Theme stored in `TenantTheme` table; served via `GET /api/tenants/{id}/theme` (public, no auth)
- [ ] Student and instructor frontends load tenant theme on init using `X-Tenant-Id` from hostname resolution
- [ ] CSS custom properties injected globally: `--color-primary`, `--color-secondary`, `--font-family`, `--logo-url`

**SSO configuration:**

- [ ] `PUT /api/tenants/{id}/sso` — org admin configures: `{ provider: SAML|OIDC, metadataUrl?, clientId?, clientSecret?, entityId? }`
- [ ] Backend calls Keycloak Admin API to create/update identity provider in the `lms` realm for this tenant
- [ ] SSO login tested via `GET /api/tenants/{id}/sso/test` — returns `{ success, error? }`

**Tenant provisioning (platform admin):**

- [ ] `POST /api/tenants` — platform admin creates tenant: `{ name, adminEmail, plan, allowedDomains[] }`
- [ ] Creates: tenant record, Keycloak group for tenant, initial org-admin user, default XP config, default task definitions
- [ ] Full provisioning completes < 60 seconds
- [ ] `GET /api/tenants` — platform admin sees all tenants with plan, user count, last active

### Key data model

```
Tenant { Id, Name, Plan, CustomDomain?, DomainStatus, AdminEmail, AllowedDomains[], CreatedAt }
TenantTheme { TenantId, LogoUrl, PrimaryColor, SecondaryColor, FontFamily, FaviconUrl, UpdatedAt }
TenantSsoConfig { TenantId, Provider, MetadataUrl?, ClientId?, EntityId?, IsActive }
```

---

## WP 9.6 — Mandatory Training Tracking

**Sprint:** 21–22 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.ComplianceService`

### Context

Enterprise customers need to assign mandatory training (e.g., annual compliance training, onboarding) and track completion against a deadline. Managers and admins see compliance status; reminders are sent automatically.

### User stories

**US-9.6.1** — As an admin, I can mark a course as mandatory for all users in a role or department so that required training is tracked centrally.

**US-9.6.2** — As an admin, I can set a completion deadline for mandatory training so that there is a clear accountability date.

**US-9.6.3** — As a manager, I can see which of my team members have not yet completed mandatory training so that I can follow up.

**US-9.6.4** — As a student, I receive reminders at 30, 14, and 3 days before my mandatory training deadline so that I don't miss it.

**US-9.6.5** — As an admin, I can export a compliance report showing completion status for all mandatory training assignments.

### Acceptance criteria

- [ ] `POST /api/compliance/assignments` — admin assigns mandatory training: `{ courseId, targetRoles[]?, targetUserIds[]?, deadline, reminderDays[30,14,3] }`; calls EnrollmentService to enroll all targets
- [ ] `GET /api/compliance/assignments` — returns all mandatory assignments with completion rates
- [ ] `GET /api/compliance/assignments/{id}/status` — returns per-user completion status: `Completed / InProgress / NotStarted / Overdue`
- [ ] `GET /api/compliance/assignments/{id}/status/export` — CSV export: `userId, name, email, status, completedAt, daysOverdue`
- [ ] Reminder job: daily; for each active assignment checks users where `deadline - today IN reminderDays`; sends `NotificationRequested` (email + in-app); idempotent per `(userId, assignmentId, reminderDay)`
- [ ] On deadline passed + not completed: status → `Overdue`; `TrainingOverdue` event published → manager notified (if manager field set on user profile)
- [ ] `GET /api/compliance/dashboard` — org admin; returns `{ totalAssignments, overdueCount, completionRate, urgentItems[]{assignment, overdueCount} }`
- [ ] Compliance report available as embeddable iframe (uses WP 8.4 embed token)

### Key data model

```
MandatoryAssignment { Id, CourseId, TenantId, CreatedBy, TargetRoles[], TargetUserIds[]?, Deadline, ReminderDays[], IsActive, CreatedAt }
ComplianceRecord { Id, AssignmentId, UserId, TenantId, Status, EnrolledAt, CompletedAt, LastReminderSentAt }
```

---

## WP 10.1 — Offline / PWA Mode

**Sprint:** 22–23 | **Owner:** Full-stack | **Effort:** 2.5w  
**Service:** Frontend (PWA) + `LMS.ProgressService` (sync endpoint)

### Context

Mobile learners in low-connectivity environments need to continue learning offline. The Progressive Web App caches lesson content and queues progress events locally, syncing when connectivity is restored.

### User stories

**US-10.1.1** — As a student on a mobile device, I can download a lesson for offline viewing so that I can learn without an internet connection.

**US-10.1.2** — As a student, my progress (lessons completed, quiz answers) is saved offline and automatically synced when I reconnect so that nothing is lost.

**US-10.1.3** — As a student, I receive a push notification reminder about my streak even when the app is not open so that I don't break my streak.

**US-10.1.4** — As a student, the app installs on my home screen like a native app so that I have quick access without a browser.

### Acceptance criteria

**PWA setup:**

- [ ] `manifest.json` with: `name`, `short_name`, `icons(192px, 512px)`, `start_url`, `display: standalone`, `theme_color`, `background_color`
- [ ] Service worker registered using Workbox; strategy: `NetworkFirst` for API calls, `CacheFirst` for static assets
- [ ] App installable on iOS (Safari) and Android (Chrome) with "Add to home screen" prompt

**Offline content:**

- [ ] Student explicitly downloads a lesson: `POST /api/offline/lessons/{id}/download` — server responds with: lesson metadata JSON + list of asset URLs to cache
- [ ] Service worker caches: lesson HTML body, video HLS segments (up to 720p, max 500MB per lesson), PDF attachment
- [ ] Offline indicator in UI when service worker detects no network
- [ ] Downloaded content listed in "My Downloads" page; student can delete individual downloads to free storage

**Offline progress sync:**

- [ ] Progress events generated offline (lesson completed, quiz submitted) stored in IndexedDB event queue
- [ ] On reconnect: service worker sends queued events to `POST /api/progress/sync` — accepts batch of up to 50 events
- [ ] Sync endpoint: `{ events: [{ type, payload, clientTimestamp }] }` — processed in order; conflicts resolved by `last-write-wins` on `completionPercent` (never decrease)
- [ ] Sync idempotent: duplicate events identified by `clientEventId` (UUID generated offline); ignored if already processed
- [ ] Sync status shown to student: "3 activities synced" toast on reconnect

**Push notifications:**

- [ ] Firebase FCM integrated for push notifications (Web Push API for browsers)
- [ ] `POST /api/notifications/push/subscribe` — student registers push subscription `{ endpoint, keys }`
- [ ] NotificationWorker sends push via FCM for: streak reminder, assignment deadline, badge earned
- [ ] Student can manage push preferences in notification settings

---

## WP 10.4 — Accessibility & i18n

**Sprint:** 22–23 | **Owner:** Full-stack | **Effort:** 2w  
**Service:** Frontend + all services (i18n strings)

### Context

WCAG 2.1 AA compliance makes the platform accessible to learners with disabilities. Internationalisation (i18n) supports RTL languages and multi-language UI strings. AI translation of course content is deferred to Phase 4 (WP 10.5).

### User stories

**US-10.4.1** — As a screen reader user, I can navigate all platform features using only a keyboard and have all content read correctly so that I have equal access to learning.

**US-10.4.2** — As a student who prefers Arabic or Hebrew, I can switch the UI to a right-to-left layout so that the platform feels natural to use.

**US-10.4.3** — As a student, I can change the platform UI language to my preferred language so that I understand all controls and labels.

**US-10.4.4** — As a hearing-impaired student, all videos have captions and all audio content has transcripts so that I can consume all learning material.

### Acceptance criteria

**WCAG 2.1 AA:**

- [ ] All interactive elements (buttons, links, form inputs) have discernible accessible names (`aria-label` or visible text)
- [ ] Focus order follows logical reading order; focus visible indicator meets 3:1 contrast ratio
- [ ] All images have `alt` text; decorative images have `alt=""`
- [ ] Color contrast: text ≥ 4.5:1, large text ≥ 3:1, UI components ≥ 3:1
- [ ] All forms have associated `<label>` elements; error messages programmatically linked via `aria-describedby`
- [ ] Modal dialogs trap focus; `Escape` closes; focus returns to trigger on close
- [ ] Video player: captions on/off toggle, keyboard-accessible controls, transcript panel
- [ ] Quiz: all question types fully operable by keyboard; radio buttons, checkboxes have correct tab order
- [ ] Automated audit: `axe-core` integrated in CI; zero violations on critical user flows (login, enroll, complete lesson)
- [ ] Manual audit: screen reader test with NVDA (Windows) and VoiceOver (macOS/iOS) before Phase 3 release

**i18n:**

- [ ] All UI strings externalised to `i18n/{locale}.json` files; no hardcoded English strings in components
- [ ] Supported locales at Phase 3 launch: `en`, `vi`, `ar`, `he`, `fr`, `de`, `ja` (expand per demand)
- [ ] `PUT /api/identity/profile/me` includes `language` preference; stored and returned with profile
- [ ] RTL support: `<html dir="rtl">` set when locale is `ar` or `he`; layout mirrors using CSS logical properties (`margin-inline-start` not `margin-left`)
- [ ] Date, time, and number formatting use `Intl` API based on user locale
- [ ] Backend API error messages returned in user's locale via `Accept-Language` header
- [ ] Translation workflow: new strings added to `en.json` → CI flags missing translations in other locales → translator fills via a simple translation portal

---

## Open Design Decisions

| ID | Decision | Blocks | Options | Recommendation |
|---|---|---|---|---|
| OD-7.2.a | Marketplace search: Elasticsearch vs PostgreSQL full-text? | WP 7.2 | Elasticsearch / PostgreSQL FTS / Typesense | Elasticsearch for faceted filtering at scale; Typesense as lighter alternative |
| OD-7.2.b | Who approves marketplace listings: dedicated curator role or any admin? | WP 7.2 | Any admin / Dedicated curator role | Dedicated `curator` role added to Keycloak; prevents admin overload |
| OD-7.3.a | Minimum payout threshold: $25 fixed or configurable per tenant? | WP 7.3 | Fixed $25 / Configurable | Configurable per tenant; default $25 |
| OD-7.6.a | Skill taxonomy: use SFIA 9 as default or custom only? | WP 7.6 | SFIA 9 seed / Custom only / Both | Both: seed SFIA 9 as a starting point; orgs can customise |
| OD-6.4.a | Peer review scoring: mean of all reviews or drop outliers? | WP 6.4 | Simple mean / Drop outliers (>2σ) | Drop outliers (>2σ) for fairness; show outlier flag to instructor |
| OD-9.1.a | GDPR erasure of forum posts: delete body or replace with placeholder? | WP 9.1 | Delete body / Replace with "[Deleted]" | Replace with "[Deleted]" to preserve thread coherence |
| OD-9.1.b | Payment records: anonymise name/email or retain for 7 years as-is? | WP 9.1 | Retain as-is (legal basis) / Anonymise non-required fields | Retain `amount`, `currency`, `date`; anonymise `name`, `email` (minimum legal requirement) |
| OD-9.4.a | PDF watermark: invisible only or offer visible option per course? | WP 9.4 | Invisible only / Configurable | Configurable per course: default invisible, visible optional |
| OD-9.5.a | TLS cert provisioning: Let's Encrypt (cert-manager) vs Cloudflare proxy? | WP 9.5 | cert-manager / Cloudflare / AWS ACM | cert-manager + Let's Encrypt (no vendor lock-in); Cloudflare as alternative if CDN already used |
| OD-10.1.a | Offline video: full download or progressive cache (first 10 min)? | WP 10.1 | Full download / Progressive 10min cache | Full download (explicit student choice, predictable storage); progressive cache for auto-cache |
| OD-10.4.a | RTL layout: CSS logical properties throughout or separate RTL stylesheet? | WP 10.4 | Logical properties / Separate stylesheet | CSS logical properties from day one; avoids maintaining two stylesheets |

---

## Phase 3 Event Contract Summary

| Event | Publisher | Phase 3 Consumers |
|---|---|---|
| `CoursePublished` | CourseService | MarketplaceService (index in Elasticsearch) |
| `CourseReviewed` | MarketplaceService | AnalyticsWorker, NotificationWorker (instructor) |
| `CourseCompleted` | ProgressService | SkillsService (update skill passport) |
| `LearnerAtRisk` | AnalyticsWorker | NotificationWorker (instructor alert) |
| `TrainingOverdue` | ComplianceService | NotificationWorker (admin + manager) |
| `PaymentProcessed` | PaymentService | RevenueWorker (earnings calculation) |
| `PeerReviewCompleted` | PeerReviewService | ProgressService, GamificationService, AnalyticsWorker |
| `MentorMatchClosed` | CohortService | GamificationService (MentorBadge evaluation) |
| `GdprErasureRequested` | GdprService | All services (per erasure pipeline) |
| `UserDataErased` | Each service | GdprService (Saga confirmation) |
| `AuditEvent` | All services | AuditLogService |
| `ContentCaptioned` | ContentService | AnalyticsWorker, NotificationWorker (instructor) |
| `GenerationJobCompleted` | AiCourseGeneratorService | NotificationWorker (instructor) |

---

## Phase 3 API Surface Summary

| Service | Base path | Auth model | Notes |
|---|---|---|---|
| MarketplaceService | `/api/marketplace` | Public for search; JWT for purchase/review | Admin approval endpoints role-gated |
| SkillsService | `/api/skills` | Taxonomy = public; passport = JWT; org = org-admin | JSON-LD export endpoint |
| PeerReviewService | `/api/peer-reviews` | JWT; enrollment required | Instructor moderation |
| CohortService | `/api/cohorts`, `/api/mentoring` | JWT; course enrollment required | SignalR for mentor messaging |
| AiCourseGeneratorService | `/api/ai-generator` | JWT; instructor only | Async job pattern |
| GdprService | `/api/gdpr` | JWT; admin for request list | Internal erasure commands |
| AuditLogService | `/api/audit-log` | JWT; admin/compliance role only | NDJSON export |
| TenantService | `/api/tenants` | Platform admin for provisioning; org admin for config | Domain verification flow |
| ComplianceService | `/api/compliance` | JWT; admin/manager role | CSV export |
| AnalyticsWorker (ext) | `/api/analytics/...` | JWT; role-gated | OData endpoint for Power BI |

---

*Document: LMS Phase 3 Feature Specification · Version 1.0 · April 2026*  
*Next: Phase 4 Feature Specification (Virtual Labs, Blockchain Credentials, Engagement AI, AI Translation)*
