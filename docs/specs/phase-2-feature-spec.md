# LMS Platform — Phase 2 Feature Specification

**Phase:** 2 — Learning Intelligence  
**Sprints:** 9–16 (16 weeks)  
**Goal:** Elevate the platform with gamification, adaptive assessment, AI tutor, live sessions, payments, and analytics. The platform becomes meaningfully differentiated from basic LMS competitors in this phase.  
**Prerequisite:** All Phase 1 work packages are complete and in production.  
**Stack:** Adds — Semantic Kernel · pgvector · Redis sorted sets · SignalR · Stripe · ClickHouse · Zoom API · Copyleaks API

---

## Table of Contents

1. [WP 5.1 — Points & XP Engine](#wp-51--points--xp-engine)
2. [WP 5.2 — Streak & Check-in System](#wp-52--streak--check-in-system)
3. [WP 5.3 — Tasks & Daily Missions](#wp-53--tasks--daily-missions)
4. [WP 5.4 — Goals (Multi-step)](#wp-54--goals-multi-step)
5. [WP 5.5 — Badges & Achievements](#wp-55--badges--achievements)
6. [WP 5.6 — SignalR Live Badge Pop-ups](#wp-56--signalr-live-badge-pop-ups)
7. [WP 6.1 — Live Session Service](#wp-61--live-session-service)
8. [WP 6.2 — Recording Ingestor](#wp-62--recording-ingestor)
9. [WP 6.3 — Discussion Forums](#wp-63--discussion-forums)
10. [WP 7.1 — Payment Service](#wp-71--payment-service)
11. [WP 8.1 — Analytics Worker (CQRS)](#wp-81--analytics-worker-cqrs)
12. [WP 8.2 — Instructor Dashboards](#wp-82--instructor-dashboards)
13. [WP 3.2 — Adaptive Assessment Engine](#wp-32--adaptive-assessment-engine)
14. [WP 3.3 — LLM Auto-grading](#wp-33--llm-auto-grading)
15. [WP 3.4 — Plagiarism Detection](#wp-34--plagiarism-detection)
16. [WP 3.5 — Grade Appeal Workflow](#wp-35--grade-appeal-workflow)
17. [WP 4.1 — Recommendation Service](#wp-41--recommendation-service)
18. [WP 4.2 — AI Tutor / RAG Chatbot](#wp-42--ai-tutor--rag-chatbot)
19. [WP 9.3 — SCORM / xAPI / cmi5 Compliance](#wp-93--scorm--xapi--cmi5-compliance)
20. [Open Design Decisions](#open-design-decisions)
21. [Phase 2 Event Contract Summary](#phase-2-event-contract-summary)
22. [Phase 2 API Surface Summary](#phase-2-api-surface-summary)

---

## WP 5.1 — Points & XP Engine

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.GamificationService`

### Context

The GamificationService is a pure event consumer. It subscribes to domain events from all other services and applies a configurable rules engine to award points, XP, and level-ups. The leaderboard is maintained as a Redis sorted set for real-time ranking.

### User stories

**US-5.1.1** — As a student, I earn XP points for completing lessons, passing quizzes, and attending live sessions so that my effort is visibly rewarded.

**US-5.1.2** — As a student, I can see my total XP, current level, and how many XP I need to reach the next level so that I have a clear progression goal.

**US-5.1.3** — As a student, I can see a leaderboard of the top students in my course or organisation so that I feel motivated to keep up.

**US-5.1.4** — As an instructor, I can configure the XP values for each event type in my course so that I can tune the reward balance.

**US-5.1.5** — As a student, I earn a bonus XP multiplier during a defined streak so that consistent learners are extra rewarded.

### Acceptance criteria

- [ ] `GET /api/gamification/me` — returns `{ xp, level, xpToNextLevel, rank, streakDays, badges[], recentEvents[] }`
- [ ] `GET /api/gamification/leaderboard?scope=course&courseId={id}` — returns top 50 students by XP for a course; scope options: `course`, `tenant`, `global`
- [ ] `GET /api/gamification/leaderboard?scope=tenant` — returns top 50 students in the current user's tenant
- [ ] XP awarded on the following events (default values, instructor-configurable per course):

| Event | Default XP |
|---|---|
| `LessonCompleted` | 10 XP |
| `CourseCompleted` | 200 XP |
| `AssessmentSubmitted` (passed) | 50 XP |
| `AssessmentSubmitted` (perfect score) | +25 XP bonus |
| `LiveSessionAttended` | 30 XP |
| `StreakMaintained` (daily) | 5 XP |

- [ ] Level thresholds (configurable in admin): L1=0, L2=100, L3=300, L4=600, L5=1000, L6=1500 … (exponential curve)
- [ ] `LevelUp` event published when student crosses a level threshold → NotificationWorker sends in-app toast
- [ ] Leaderboard stored as Redis ZADD sorted set: key = `leaderboard:{scope}:{scopeId}`, score = total XP, member = `userId`
- [ ] Leaderboard updates are async (consumer publishes to leaderboard set after XP award)
- [ ] XP transactions are stored immutably in `XpTransaction` table; total XP = sum of all transactions per user
- [ ] Instructor-configured XP values stored per `CourseXpConfig { CourseId, EventType, XpValue }`; falls back to global defaults
- [ ] Streak multiplier: active streak of 7+ days applies 1.5x multiplier to all XP awards

### Key data model

```
XpTransaction { Id, UserId, TenantId, CourseId?, EventType, XpAwarded, Multiplier, SourceEventId, OccurredAt }
LearnerLevel { UserId, TenantId, TotalXp, CurrentLevel, UpdatedAt }
CourseXpConfig { CourseId, TenantId, EventType, XpValue }
```

---

## WP 5.2 — Streak & Check-in System

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.GamificationService`

### Context

A streak is the number of consecutive calendar days a student has been active on the platform. A freeze token allows a student to preserve their streak for one missed day (grace day). The check-in is triggered automatically by any learning activity, not a manual action.

### User stories

**US-5.2.1** — As a student, my streak increases by 1 for every consecutive calendar day I complete at least one lesson or activity so that I am rewarded for consistency.

**US-5.2.2** — As a student, I can see my current streak count and my all-time best streak on my profile.

**US-5.2.3** — As a student, I can use a freeze token to protect my streak if I miss a day so that occasional absences don't break long streaks.

**US-5.2.4** — As a student, I receive a push notification reminder if I haven't been active by 7pm in my local timezone so that I don't accidentally break my streak.

**US-5.2.5** — As a student, I earn freeze tokens as rewards for achieving streak milestones so that tokens are earned, not purchased.

### Acceptance criteria

- [ ] `GET /api/gamification/streak` — returns `{ currentStreak, longestStreak, lastActivityDate, freezeTokens, checkInHistory[30] }`
- [ ] `POST /api/gamification/streak/freeze` — uses one freeze token to mark yesterday as a grace day; fails if no tokens available or streak already broken by more than 1 day
- [ ] Streak logic:
  - Activity today AND yesterday → streak increments
  - Activity today, none yesterday but freeze token used → streak preserved
  - No activity today or yesterday → streak resets to 0; `StreakBroken` event published
  - Activity today but yesterday already counted → no change (idempotent)
- [ ] Streak check-in triggered by consuming `LessonCompleted`, `AssessmentSubmitted`, or `LiveSessionAttended`
- [ ] Daily reminder job: runs at 7pm per-user local timezone; sends `NotificationRequested` only if no activity recorded today; respects user notification preferences
- [ ] Freeze token milestones: 7-day streak = 1 token, 30-day streak = 2 tokens, 100-day streak = 5 tokens
- [ ] `StreakMaintained` event published each day a streak is continued → awards 5 XP (WP 5.1)
- [ ] `StreakBroken` event published → NotificationWorker sends sympathetic "Your streak ended" message

### Key data model

```
LearnerStreak { UserId, TenantId, CurrentStreak, LongestStreak, LastActivityDate, FreezeTokens, UpdatedAt }
StreakCheckIn { Id, UserId, TenantId, Date, IsGraceDay, OccurredAt }
```

---

## WP 5.3 — Tasks & Daily Missions

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.GamificationService`

### Context

Tasks are short, repeatable objectives that reset on a daily or weekly cadence. They give students a clear "what should I do today" directive. Tasks are defined globally by admins or per-course by instructors.

### User stories

**US-5.3.1** — As a student, I see a set of daily tasks each morning so that I always know what to do next.

**US-5.3.2** — As a student, completing a task rewards me with XP or a badge so that tasks feel worth doing.

**US-5.3.3** — As an instructor, I can define custom tasks for my course so that I guide students towards specific activities.

**US-5.3.4** — As a student, I can see which tasks I've completed today and which are still pending so that I can track my daily progress.

### Acceptance criteria

- [ ] `GET /api/gamification/tasks` — returns `{ daily: Task[], weekly: Task[] }` each with `{ id, title, description, xpReward, progress, target, completedAt?, resetAt }`
- [ ] Tasks reset automatically: daily tasks at midnight UTC, weekly tasks on Monday midnight UTC
- [ ] Task progress is updated by consuming relevant domain events (e.g., `LessonCompleted` increments "Complete 3 lessons today" task)
- [ ] On task completion: XP awarded via XpTransaction, `TaskCompleted` event published
- [ ] Default global daily tasks (always present):

| Task | Target | XP reward |
|---|---|---|
| Complete a lesson | 1 lesson | 15 XP |
| Watch 20 minutes of video | 20 min | 20 XP |
| Answer a quiz question | 1 question | 10 XP |
| Log in and be active | 1 session | 5 XP |

- [ ] Instructors can add course-specific tasks via `POST /api/gamification/tasks` (instructor role, scoped to courseId)
- [ ] Task definitions stored in DB; task progress stored per user per day in Redis (key = `tasks:{userId}:{date}`)
- [ ] Completed tasks cannot be re-completed within the same reset window (idempotent)

### Key data model

```
TaskDefinition { Id, TenantId, CourseId?, Title, Description, Cadence(Daily/Weekly), TargetEventType, TargetCount, XpReward, IsGlobal, CreatedBy }
TaskProgress { Id, UserId, TaskDefinitionId, TenantId, Progress, Target, CompletedAt, WindowStart, WindowEnd }
```

---

## WP 5.4 — Goals (Multi-step)

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.GamificationService`

### Context

Goals are longer-horizon objectives with multiple milestones that span days or weeks. They can be set by the student themselves ("I want to complete 5 courses this month") or assigned by an instructor or org admin ("All new hires must complete the onboarding path in 30 days").

### User stories

**US-5.4.1** — As a student, I can set a personal learning goal with a target and deadline so that I have something concrete to work towards.

**US-5.4.2** — As a student, I can see a progress bar for each of my active goals so that I always know how close I am.

**US-5.4.3** — As an instructor, I can assign a goal to all students in my course so that they have a clear target.

**US-5.4.4** — As a student, I receive a notification when I am at risk of missing a goal deadline so that I can catch up in time.

**US-5.4.5** — As a student, completing a goal awards me XP and a badge so that the achievement is celebrated.

### Acceptance criteria

- [ ] `POST /api/gamification/goals` — create personal goal: `{ title, targetType(CoursesCompleted|LessonsCompleted|XpEarned|AssessmentsPassed), targetValue, deadline }`
- [ ] `GET /api/gamification/goals` — returns active and completed goals with `{ progress, target, percent, deadline, daysRemaining, status }`
- [ ] `DELETE /api/gamification/goals/{id}` — student can abandon their own personal goal
- [ ] `POST /api/gamification/goals/assign` — instructor/admin assigns goal to all students in a course or tenant
- [ ] Goal progress updated automatically by consuming domain events
- [ ] At-risk alert: a daily job checks all active goals; if `daysRemaining <= 3` and `percent < 80%` → send `NotificationRequested`
- [ ] On `GoalCompleted`: award XP (configurable, default 100 XP), publish `GoalCompleted` event → Badges rules engine evaluates
- [ ] Assigned goals cannot be deleted by students; only abandoned (marked as `Abandoned` status)
- [ ] Goal status enum: `Active`, `Completed`, `Abandoned`, `Missed` (past deadline, incomplete)

### Key data model

```
Goal { Id, UserId, TenantId, CourseId?, AssignedBy?, Title, TargetType, TargetValue, Deadline, Status, CompletedAt, CreatedAt }
GoalProgress { GoalId, UserId, CurrentValue, LastUpdatedAt }
```

---

## WP 5.5 — Badges & Achievements

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 1.5w  
**Service:** `LMS.GamificationService`

### Context

Badges are awarded for reaching specific milestones. Each badge has a condition defined as a rule in the rules engine. The rules engine evaluates after every relevant domain event. Badges have rarity tiers that signal their prestige.

### User stories

**US-5.5.1** — As a student, I earn badges for significant milestones so that my achievements are visually recognised on my profile.

**US-5.5.2** — As a student, I can see all available badges and my progress towards unearned ones so that I have something to aim for.

**US-5.5.3** — As an instructor, I can create custom badges for my course so that I can reward course-specific achievements.

**US-5.5.4** — As a student, I receive a notification and a visual pop-up when I earn a new badge so that the moment feels special.

### Acceptance criteria

- [ ] `GET /api/gamification/badges` — returns all badges: earned (with `earnedAt`) and unearned (with `progress` towards condition)
- [ ] `GET /api/gamification/badges/me` — returns only earned badges for current user
- [ ] `POST /api/gamification/badges` — instructor/admin creates custom badge: `{ name, description, iconUrl, rarity, condition{ type, threshold } }`
- [ ] On `AchievementUnlocked`: publish event → NotificationWorker sends in-app toast + email (if preference set)

**Built-in badge library (Phase 2):**

| Badge | Condition | Rarity |
|---|---|---|
| `FirstStep` | Complete first lesson | Common |
| `CourseFinisher` | Complete any course | Common |
| `QuizAce` | Score 100% on any quiz | Rare |
| `WeekStreak7` | Maintain 7-day streak | Common |
| `MonthStreak30` | Maintain 30-day streak | Rare |
| `Century100` | Maintain 100-day streak | Epic |
| `SpeedRunner` | Complete a course in < 3 days | Rare |
| `EarlyBird` | Log in before 7am local time 5 days in a row | Rare |
| `PerfectPath` | Complete all lessons in a course with 100% quiz avg | Epic |
| `TripleCourse` | Complete 3 courses | Common |
| `TenCourses` | Complete 10 courses | Rare |
| `LiveAttendee` | Attend 5 live sessions | Common |
| `HelpfulPeer` | Have a forum post upvoted 10 times | Rare |
| `TopOfClass` | Rank #1 in course leaderboard for 1 week | Epic |
| `LegendaryLearner` | Reach Level 10 | Legendary |

- [ ] Rules engine evaluates badge conditions on every relevant event; uses current learner state from DB
- [ ] Badge rarity tiers: `Common`, `Rare`, `Epic`, `Legendary`
- [ ] Each badge awarded only once per user (idempotent)
- [ ] `AchievementUnlocked` event published: `{ UserId, BadgeId, BadgeName, Rarity, TenantId, OccurredAt }`
- [ ] Badge icon stored as URL to ContentService; default icons provided for all built-in badges

### Key data model

```
BadgeDefinition { Id, TenantId?, CourseId?, Name, Description, IconUrl, Rarity, ConditionType, ConditionThreshold, IsBuiltIn }
EarnedBadge { Id, UserId, BadgeDefinitionId, TenantId, EarnedAt }
```

---

## WP 5.6 — SignalR Live Badge Pop-ups

**Sprint:** 10 | **Owner:** Frontend | **Effort:** 0.5w  
**Service:** `LMS.GamificationService` (adds SignalR hub)

### Context

When a student earns a badge or levels up, they should see a real-time pop-up in the browser without needing to refresh. This is achieved via a SignalR hub in the GamificationService that pushes events to connected clients.

### User stories

**US-5.6.1** — As a student, when I earn a badge I immediately see a pop-up notification in the corner of the screen so that the moment is celebrated in real time.

**US-5.6.2** — As a student, when I level up I see an animated level-up banner so that the achievement feels significant.

**US-5.6.3** — As a student, my XP counter updates in real time without a page refresh so that I see the reward immediately after completing a lesson.

### Acceptance criteria

- [ ] SignalR hub at `/hubs/gamification` in GamificationService; authenticated via JWT query parameter (`?access_token=`)
- [ ] Hub groups: users are added to group `user:{userId}` on connect
- [ ] Server pushes the following SignalR messages to the user's group:
  - `BadgeEarned { badgeId, badgeName, iconUrl, rarity, xpAwarded }`
  - `LevelUp { newLevel, totalXp, xpToNextLevel }`
  - `XpAwarded { amount, newTotal, reason }`
  - `StreakUpdated { currentStreak, freezeTokens }`
- [ ] Client (student app) connects to hub on login; reconnects automatically on disconnect
- [ ] Pop-up design: bottom-right toast, 4-second display, badge icon + name + XP value
- [ ] Level-up: full-screen overlay with animation, dismissible
- [ ] If student is offline, events are stored in `PendingPush` table and delivered on next connection (up to 7 days)
- [ ] Aspire registers SignalR with Redis backplane (`AddSignalR().AddStackExchangeRedis()`) to support multi-pod deployment

---

## WP 6.1 — Live Session Service

**Sprint:** 11–12 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.LiveSessionService`

### Context

Live sessions are scheduled video meetings (Zoom or Google Meet) attached to a course. The LiveSessionService manages scheduling, attendance tracking, and webhook handling. It does not stream video — that is handled entirely by the third-party provider.

### User stories

**US-6.1.1** — As an instructor, I can schedule a live session for my course with a title, description, start time, and duration so that students can plan to attend.

**US-6.1.2** — As a student, I can see all upcoming live sessions for my enrolled courses on my dashboard.

**US-6.1.3** — As a student, I receive a reminder notification 24 hours and 15 minutes before a session starts.

**US-6.1.4** — As a student, clicking "Join" takes me directly to the Zoom/Google Meet room without needing to copy a link.

**US-6.1.5** — As a system, when a session ends attendance is automatically recorded based on Zoom participant data.

**US-6.1.6** — As an instructor, I can see who attended a live session and for how long so that I can track participation.

### Acceptance criteria

- [ ] `POST /api/live-sessions` — instructor creates session: `{ courseId, title, description, scheduledAt, durationMinutes, provider: Zoom|GoogleMeet }`; creates meeting via provider API; returns `{ sessionId, joinUrl, hostUrl }`
- [ ] `GET /api/live-sessions?courseId={id}` — returns upcoming and past sessions for a course
- [ ] `GET /api/live-sessions/me/upcoming` — returns all upcoming sessions across enrolled courses for current student
- [ ] `PUT /api/live-sessions/{id}` — instructor updates title/time (updates meeting via provider API)
- [ ] `DELETE /api/live-sessions/{id}` — instructor cancels session; notifies enrolled students via NotificationWorker
- [ ] `GET /api/live-sessions/{id}/attendance` — instructor only; returns list of attendees with join/leave times and duration
- [ ] `POST /api/live-sessions/webhooks/zoom` — receives Zoom webhook; validates signature; handles:
  - `meeting.started` → update session status to `Live`
  - `meeting.ended` → update status to `Ended`; trigger recording ingest; calculate attendance
  - `meeting.participant_joined` → record join time
  - `meeting.participant_left` → record leave time; calculate duration
- [ ] `LiveSessionAttended` event published for each participant who attended ≥ 10 minutes
- [ ] Reminder job: runs every 5 minutes; sends `NotificationRequested` for sessions starting in 24h (±5min) and 15min (±2min); idempotent (uses sent flag)
- [ ] Provider credentials (Zoom API key/secret or Google OAuth) stored in Azure Key Vault, per tenant

### Key data model

```
LiveSession { Id, CourseId, TenantId, InstructorId, Title, Description, ScheduledAt, DurationMinutes, Provider, ProviderMeetingId, JoinUrl, HostUrl, Status(Scheduled/Live/Ended/Cancelled), CreatedAt }
SessionAttendance { Id, SessionId, UserId, TenantId, JoinedAt, LeftAt, DurationMinutes, IsEligible(>=10min) }
```

---

## WP 6.2 — Recording Ingestor

**Sprint:** 11–12 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.LiveSessionService` (background worker)

### Context

When a live session ends, Zoom/Google Meet provides a recording download URL. The recording ingestor downloads the file and uploads it to ContentService so it appears alongside course content for students who missed the session.

### User stories

**US-6.2.1** — As a student who missed a live session, I can watch the recording from the course content area so that I don't fall behind.

**US-6.2.2** — As an instructor, I can choose whether a session recording is automatically published or held for review before students can see it.

**US-6.2.3** — As a system, the recording is automatically processed (transcoded to HLS) via the existing ContentService pipeline so that it streams adaptively.

### Acceptance criteria

- [ ] On `meeting.ended` webhook: if recording URL available, queue recording download job
- [ ] Recording download: streams from provider URL directly to S3 (avoid local disk); chunked streaming upload
- [ ] On upload complete: calls `POST /api/content/{id}/process` on ContentService to trigger HLS transcoding
- [ ] Recording linked to `LiveSession` record with `ContentItemId`
- [ ] `GET /api/live-sessions/{id}/recording` — returns signed stream URL (proxied from ContentService); requires enrollment check
- [ ] Instructor setting `AutoPublishRecording` (default: true); if false, recording status = `PendingReview`; instructor must approve via `POST /api/live-sessions/{id}/recording/publish`
- [ ] Zoom recording URL expires after 24h — ingestor must start download within 2 hours of `meeting.ended` webhook
- [ ] If download fails: retry 3 times with 15-minute intervals; alert instructor on final failure

---

## WP 6.3 — Discussion Forums

**Sprint:** 11–12 | **Owner:** Full-stack | **Effort:** 2w  
**Service:** `LMS.ForumService`

### Context

Each course has its own discussion forum with threaded posts. Instructors can pin announcements. Students can upvote posts. Moderators (instructors/admins) can delete or hide posts.

### User stories

**US-6.3.1** — As a student, I can post a question or comment in a course forum so that I can get help from peers and instructors.

**US-6.3.2** — As a student, I can reply to an existing post to create a threaded discussion.

**US-6.3.3** — As a student, I can upvote helpful posts so that the best answers rise to the top.

**US-6.3.4** — As an instructor, I can pin a post to the top of the forum so that important announcements are always visible.

**US-6.3.5** — As an instructor, I can delete or hide inappropriate posts so that I maintain a safe learning environment.

**US-6.3.6** — As a student, I receive a notification when someone replies to my post so that I can follow the conversation.

### Acceptance criteria

- [ ] `POST /api/forums/courses/{courseId}/posts` — create post: `{ title, body(markdown), isPinned? }`; requires enrollment
- [ ] `GET /api/forums/courses/{courseId}/posts` — returns paginated posts; sorts: `newest`, `top`(by votes), `unanswered`; pinned posts always first
- [ ] `GET /api/forums/posts/{postId}` — returns post with full thread of replies
- [ ] `POST /api/forums/posts/{postId}/replies` — add reply to thread (max depth: 2 levels)
- [ ] `POST /api/forums/posts/{postId}/upvote` — toggle upvote; returns new vote count; one upvote per user
- [ ] `DELETE /api/forums/posts/{postId}` — author or moderator only; soft delete (content replaced with "[deleted]")
- [ ] `PATCH /api/forums/posts/{postId}/pin` — instructor only; toggles pin status
- [ ] `PATCH /api/forums/posts/{postId}/hide` — instructor/admin only; hides post from students but preserves in DB
- [ ] On new reply to a post: publish `NotificationRequested` to post author
- [ ] Forum post body supports Markdown; sanitised server-side before storage (no raw HTML)
- [ ] Vote count cached in Redis; DB as source of truth updated async
- [ ] `HelpfulPeer` badge condition: upvotes on a user's posts across all forums (tracked in GamificationService via `PostUpvoted` event)

### Key data model

```
ForumPost { Id, CourseId, TenantId, AuthorId, Title, Body, IsPinned, IsHidden, VoteCount, ParentPostId?, CreatedAt, UpdatedAt }
ForumVote { PostId, UserId, TenantId, CreatedAt }
```

---

## WP 7.1 — Payment Service

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.PaymentService`

### Context

`LMS.PaymentService` integrates with Stripe to handle course purchases and subscription plans. In Phase 2 all paid enrollments go through this service. Instructor revenue sharing (Stripe Connect) is deferred to Phase 3 (WP 7.3).

### User stories

**US-7.1.1** — As a student, I can purchase access to a paid course using a credit card so that I can enroll.

**US-7.1.2** — As a student, I can subscribe to a monthly or annual plan that gives me access to all courses so that I have a cost-effective option.

**US-7.1.3** — As a student, I receive a receipt email after a successful payment.

**US-7.1.4** — As an admin, I can set a course as free, one-time purchase, or subscription-only so that I have pricing flexibility.

**US-7.1.5** — As a student, if my payment fails I am notified and my enrollment remains in `Pending` status until payment succeeds.

**US-7.1.6** — As an admin, I can issue a refund for a purchase within 14 days so that students are protected.

### Acceptance criteria

- [ ] `POST /api/payments/checkout` — creates Stripe Checkout Session for `{ courseId, priceId }` or subscription plan; returns `{ checkoutUrl }` (redirect to Stripe-hosted page)
- [ ] `POST /api/payments/webhooks/stripe` — receives Stripe webhook; validates signature with `STRIPE_WEBHOOK_SECRET`:
  - `checkout.session.completed` → publish `PaymentProcessed`; EnrollmentService activates enrollment
  - `invoice.payment_failed` → publish `PaymentFailed`; NotificationWorker sends email
  - `customer.subscription.deleted` → publish `SubscriptionCancelled`; EnrollmentService suspends enrollment
- [ ] `GET /api/payments/subscriptions/me` — returns current subscription status, plan, renewal date
- [ ] `POST /api/payments/subscriptions/cancel` — cancels at period end (not immediate)
- [ ] `GET /api/payments/invoices/me` — returns paginated invoice history with download URL for each PDF
- [ ] `POST /api/payments/refunds` — admin only; issues Stripe refund for `{ paymentIntentId }`; within 14-day window
- [ ] Stripe Customer ID stored per user (`StripeCustomerId` in PaymentService DB); created on first checkout
- [ ] All Stripe API calls are idempotent (use `Idempotency-Key` header = `{userId}:{courseId}:{timestamp}`)
- [ ] No card numbers ever touch LMS servers — all payment data stays within Stripe
- [ ] `PaymentProcessed` event: `{ UserId, CourseId?, PlanId?, Amount, Currency, StripePaymentIntentId, TenantId, OccurredAt }`

### Key data model

```
StripeCustomer { UserId, TenantId, StripeCustomerId }
Purchase { Id, UserId, CourseId, TenantId, StripePaymentIntentId, Amount, Currency, Status, RefundedAt?, CreatedAt }
Subscription { Id, UserId, TenantId, StripeSubscriptionId, PlanId, Status, CurrentPeriodEnd, CancelledAt? }
```

---

## WP 8.1 — Analytics Worker (CQRS)

**Sprint:** 11–12 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.AnalyticsWorker`

### Context

The AnalyticsWorker is a MassTransit consumer that listens to all domain events and projects them into a read-optimised analytics store. It is entirely separate from the transactional services — no analytics query ever touches a transactional database. ClickHouse is the recommended analytics store for its columnar query performance.

### User stories

**US-8.1.1** — As a system, all domain events are captured and stored in an analytics store so that reporting never degrades transactional service performance.

**US-8.1.2** — As an admin, I can query engagement metrics (DAU, WAU, MAU) without impacting course delivery.

**US-8.1.3** — As a system, the analytics store retains data for at least 2 years for trend analysis.

### Acceptance criteria

- [ ] Consumes and projects the following events into ClickHouse tables:

| Event | ClickHouse table | Key fields stored |
|---|---|---|
| `UserEnrolled` | `fact_enrollments` | userId, courseId, tenantId, enrolledAt, planType |
| `LessonCompleted` | `fact_lesson_completions` | userId, lessonId, courseId, tenantId, watchPercent, occurredAt |
| `CourseCompleted` | `fact_course_completions` | userId, courseId, tenantId, occurredAt |
| `AssessmentSubmitted` | `fact_assessments` | userId, assessmentId, score, passed, tenantId, occurredAt |
| `LiveSessionAttended` | `fact_live_attendance` | userId, sessionId, courseId, durationMinutes, tenantId, occurredAt |
| `AchievementUnlocked` | `fact_achievements` | userId, badgeId, rarity, tenantId, occurredAt |
| `PaymentProcessed` | `fact_payments` | userId, courseId, amount, currency, tenantId, occurredAt |

- [ ] Projections are idempotent — duplicate events (identified by `SourceEventId`) are deduplicated
- [ ] ClickHouse write latency: events projected within 10 seconds of being published to bus
- [ ] `GET /api/analytics/platform` — admin only; returns `{ dau, wau, mau, newEnrollments, courseCompletions, revenue }` for configurable date range
- [ ] Materialized views in ClickHouse pre-aggregate daily/weekly/monthly rollups for fast dashboard queries
- [ ] Analytics Worker has no outbound HTTP calls to transactional services — it only reads the bus and writes to ClickHouse

---

## WP 8.2 — Instructor Dashboards

**Sprint:** 11–12 | **Owner:** Full-stack | **Effort:** 2w  
**Service:** Analytics queries served by `LMS.AnalyticsWorker` API

### Context

Instructor dashboards give instructors actionable insight into their course performance. All data is read from ClickHouse via the AnalyticsWorker API — never from transactional services directly.

### User stories

**US-8.2.1** — As an instructor, I can see how many students have enrolled in each of my courses over time so that I understand growth.

**US-8.2.2** — As an instructor, I can see the completion funnel for my course so that I know exactly which lesson students drop off at.

**US-8.2.3** — As an instructor, I can see per-lesson average watch time and completion rate so that I identify weak content.

**US-8.2.4** — As an instructor, I can see per-question correct answer rates for my quizzes so that I identify confusing questions.

**US-8.2.5** — As an instructor, I can see which students have not been active in the past 7 days so that I can proactively reach out.

### Acceptance criteria

- [ ] `GET /api/analytics/instructor/courses/{courseId}/overview` — returns: `{ totalEnrolled, completionRate, avgScore, avgWatchPercent, activeLastWeek, dropOffLesson }`
- [ ] `GET /api/analytics/instructor/courses/{courseId}/funnel` — returns ordered lessons with `{ lessonId, title, startedCount, completedCount, dropOffRate }`
- [ ] `GET /api/analytics/instructor/courses/{courseId}/lessons/{lessonId}` — returns: `{ avgWatchPercent, completionRate, avgTimeSpentSeconds, replayRate }`
- [ ] `GET /api/analytics/instructor/courses/{courseId}/assessments/{assessmentId}` — returns per-question: `{ questionId, prompt, correctRate, mostSelectedWrongOption }`
- [ ] `GET /api/analytics/instructor/courses/{courseId}/students/inactive` — returns students with no activity in past N days (default 7); includes last active date
- [ ] `GET /api/analytics/instructor/courses/{courseId}/enrollments/timeseries` — daily enrollment count for past 90 days
- [ ] All endpoints enforce instructor ownership: instructor can only query their own courses
- [ ] Responses cached in Redis for 15 minutes to prevent repeated ClickHouse scans
- [ ] Date range filter available on all time-series endpoints: `?from=2026-01-01&to=2026-04-01`

---

## WP 3.2 — Adaptive Assessment Engine

**Sprint:** 13–14 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.AssessmentService` (extension)

### Context

The adaptive engine extends the existing AssessmentService. Instead of serving questions in a fixed order, it uses a simplified Item Response Theory (IRT) model to estimate the learner's ability (`theta`) in real time and selects the next question closest to that estimated difficulty.

### User stories

**US-3.2.1** — As a student, adaptive quizzes adjust their difficulty to my level so that I am always appropriately challenged — not bored and not overwhelmed.

**US-3.2.2** — As an instructor, I can mark a quiz as "adaptive" and assign a difficulty rating (1–5) to each question so that the engine has the data it needs.

**US-3.2.3** — As a student, an adaptive quiz ends when the engine is confident in my ability estimate (not after a fixed number of questions) so that I don't answer unnecessary questions.

**US-3.2.4** — As an instructor, I can see the ability distribution of my students across an adaptive assessment so that I understand the class spread.

### Acceptance criteria

- [ ] `Assessment.IsAdaptive` flag added to assessment schema
- [ ] Each `Question` gains a `DifficultyRating` field (1.0–5.0, decimal)
- [ ] Adaptive session flow:
  1. Session starts with `theta = 0.5` (mid-range prior)
  2. Question selected: closest difficulty to current `theta` from unanswered questions
  3. Student answers; IRT update: if correct → `theta += 0.1 * (1 - P(correct|theta, difficulty))`; if wrong → `theta -= 0.1 * P(correct|theta, difficulty)`
  4. Session ends when: `|thetaChange| < 0.05` for last 3 questions OR all questions exhausted OR max questions reached
- [ ] `theta` stored in Redis session state; updated after each answer
- [ ] Final `theta` stored in `AssessmentAttempt.AbilityEstimate`
- [ ] `GET /api/assessments/{id}/analytics/ability-distribution` — instructor only; returns histogram of `AbilityEstimate` values across all attempts
- [ ] Adaptive sessions cannot be paused and resumed (timer continues in background)
- [ ] Backward compatible: non-adaptive assessments continue to work exactly as Phase 1

---

## WP 3.3 — LLM Auto-grading

**Sprint:** 13–14 | **Owner:** AI Team | **Effort:** 2.5w  
**Service:** `LMS.AssessmentService` (extension)

### Context

In Phase 1, only MCQ and true/false were auto-graded. Phase 2 adds auto-grading for short-answer and essay questions using an LLM (Anthropic Claude or OpenAI GPT-4) against an instructor-defined rubric. A human review gate is mandatory before grades are released to students.

### User stories

**US-3.3.1** — As an instructor, I can create short-answer and essay questions with a grading rubric so that longer responses can be assessed.

**US-3.3.2** — As a system, short-answer and essay submissions are automatically scored against the rubric by an AI so that instructors are not overwhelmed with manual grading.

**US-3.3.3** — As an instructor, I review the AI's suggested grade and feedback before it is released to the student so that I maintain academic control.

**US-3.3.4** — As a student, I receive personalised written feedback on my essay, not just a score, so that I understand how to improve.

**US-3.3.5** — As an instructor, I can override the AI's grade with my own score so that the final grade always reflects my judgement.

### Acceptance criteria

- [ ] New question types added: `ShortAnswer` (< 200 words), `Essay` (< 2000 words)
- [ ] Instructor defines rubric per question: `{ criteria: [{ name, description, maxPoints }] }`
- [ ] On essay submission: `AssessmentService` publishes `EssaySubmitted` event; `GradingWorker` consumes it
- [ ] `GradingWorker` calls LLM API with: question prompt, student answer, rubric criteria → receives: `{ score, criteriaScores[], feedbackText }`
- [ ] LLM prompt is hardened: instructs model to grade only against rubric, not add unsolicited commentary
- [ ] AI grade stored as `{ suggestedScore, criteriaBreakdown, feedbackText, gradedBy: "AI", status: "PendingReview" }`
- [ ] `GET /api/assessments/grading/queue` — instructor only; returns all submissions pending review sorted by oldest first
- [ ] `POST /api/assessments/grading/{submissionId}/approve` — instructor approves AI grade; status → `Released`; student notified
- [ ] `POST /api/assessments/grading/{submissionId}/override` — instructor sets `{ finalScore, feedbackText }`; status → `Released`; original AI grade preserved in audit log
- [ ] `GET /api/assessments/attempts/{id}/feedback` — student only; returns feedback if status = `Released`
- [ ] LLM API key stored in Azure Key Vault; model configurable per tenant (allow tenants to use own API key)
- [ ] Cost guardrail: max 4000 tokens per grading call; essays truncated with warning if exceeded

---

## WP 3.4 — Plagiarism Detection

**Sprint:** 13–14 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.PlagiarismWorker`

### Context

Plagiarism detection runs asynchronously after an essay or assignment is submitted. Two checks run in parallel: an internal similarity check using pgvector embeddings against past submissions in the same course, and an optional external check via Copyleaks or Turnitin webhook.

### User stories

**US-3.4.1** — As an instructor, I am alerted if a student's essay is suspiciously similar to a past submission so that I can investigate.

**US-3.4.2** — As an instructor, I can see a similarity report showing which sections of the essay match other sources so that I have evidence.

**US-3.4.3** — As an admin, I can configure the similarity threshold (%) that triggers an instructor alert per course.

**US-3.4.4** — As a student, I am not accused of plagiarism based on automated detection alone — all flags require instructor review so that false positives don't harm me.

### Acceptance criteria

- [ ] Consumes `EssaySubmitted` event; runs two checks in parallel:
  1. **Internal check:** generate text embedding via OpenAI/Anthropic embeddings API → store in pgvector → query top-5 most similar past submissions → compute cosine similarity scores
  2. **External check (optional, per tenant config):** submit to Copyleaks API; await webhook callback `POST /api/plagiarism/webhooks/copyleaks`
- [ ] `PlagiarismReport` created with: `{ submissionId, internalSimilarityScore, externalSimilarityScore?, matchedSubmissions[], flagLevel, status }`
- [ ] Flag levels: `Clear` (< 20%), `Warning` (20–40%), `Flag` (> 40%)
- [ ] If `flagLevel = Flag`: publish `PlagiarismFlagged` → NotificationWorker alerts instructor with link to report
- [ ] `GET /api/plagiarism/reports/{submissionId}` — instructor only; returns full report
- [ ] `POST /api/plagiarism/reports/{submissionId}/dismiss` — instructor dismisses flag; records reason; student unaffected
- [ ] `POST /api/plagiarism/reports/{submissionId}/escalate` — instructor escalates; creates an `AcademicIntegrityCase` record for admin review
- [ ] Threshold configurable per course: `POST /api/plagiarism/config` (instructor); defaults: Warning=20%, Flag=40%
- [ ] Embeddings stored in `submission_embeddings` table with pgvector; indexed with `ivfflat` for ANN search
- [ ] External check is optional: if `ExternalProviderEnabled = false` on tenant config, only internal check runs
- [ ] Student is never notified of a plagiarism flag until instructor takes action

---

## WP 3.5 — Grade Appeal Workflow

**Sprint:** 14 | **Owner:** Backend | **Effort:** 1w  
**Service:** `LMS.AssessmentService` (extension)

### Context

Students can appeal a grade they believe is incorrect. Appeals go through a structured workflow with an audit trail. The instructor reviews and responds; if escalated, an admin makes the final decision.

### User stories

**US-3.5.1** — As a student, I can submit a grade appeal with a written justification so that I have recourse if I believe my grade is wrong.

**US-3.5.2** — As an instructor, I receive a notification when a student appeals my grade so that I can review it promptly.

**US-3.5.3** — As an instructor, I can uphold or change a grade in response to an appeal so that I have final say.

**US-3.5.4** — As a student, I can escalate an appeal to an admin if I believe the instructor's decision is unfair.

**US-3.5.5** — As a system, every action in an appeal is logged with a timestamp and actor so that there is a full audit trail.

### Acceptance criteria

- [ ] `POST /api/assessments/attempts/{id}/appeal` — student submits appeal: `{ justification }` within 14 days of grade release
- [ ] `GET /api/assessments/appeals` — instructor sees all open appeals for their courses; admin sees all
- [ ] `GET /api/assessments/appeals/{id}` — returns appeal with full event log
- [ ] `POST /api/assessments/appeals/{id}/respond` — instructor: `{ decision: Uphold|Revise, revisedScore?, responseText }`
- [ ] `POST /api/assessments/appeals/{id}/escalate` — student escalates after instructor upholds; requires `{ escalationReason }`
- [ ] `POST /api/assessments/appeals/{id}/resolve` — admin only; final decision; grade updated if revised
- [ ] Appeal states: `Submitted → InstructorReview → [Resolved | EscalatedToAdmin] → Resolved`
- [ ] All state transitions logged to `AppealEvent { AppealId, ActorId, Action, Notes, OccurredAt }`
- [ ] A student can only appeal each attempt once
- [ ] `NotificationRequested` published on every state transition to relevant parties

---

## WP 4.1 — Recommendation Service

**Sprint:** 15–16 | **Owner:** AI Team | **Effort:** 3w  
**Service:** `LMS.RecommendationService`

### Context

The RecommendationService maintains a per-learner knowledge graph of topic mastery and uses it to recommend the next best learning action: the next course, a remedial lesson, or a stretch goal. It is a pure consumer — it never makes outbound HTTP calls to other services during the recommendation generation.

### User stories

**US-4.1.1** — As a student, I see a "Recommended for you" section on my dashboard with courses and lessons personalised to my progress so that I always know what to learn next.

**US-4.1.2** — As a student, if I struggle with a topic (low quiz scores, low watch completion), I am recommended remedial content for that topic so that gaps are filled.

**US-4.1.3** — As a student, the recommendations change as I progress so that I am never shown content I have already completed.

**US-4.1.4** — As an instructor, I can tag courses and lessons with topic slugs so that the recommendation engine understands the subject matter.

**US-4.1.5** — As a student, I receive a spaced repetition reminder for a topic I haven't revisited in a while so that I retain what I've learned.

### Acceptance criteria

- [ ] `GET /api/recommendations/me` — returns `{ nextLesson?, recommendedCourses[], remedialSuggestions[], spacedRepetitionItems[] }`
- [ ] `GET /api/recommendations/me/path` — returns an ordered list of suggested next steps (lesson or course) with reasoning
- [ ] Recommendation engine runs on consuming the following events: `LessonCompleted`, `CourseCompleted`, `AssessmentSubmitted`
- [ ] Knowledge graph: `LearnerMastery { UserId, TopicSlug, MasteryScore(0-1), RepetitionCount, LastStudiedAt, NextReviewAt }`
- [ ] Mastery score update rules:
  - `LessonCompleted` with `watchPercent > 80%` → `masteryScore += 0.1`
  - `AssessmentSubmitted` passed → `masteryScore += 0.15 * (score / maxScore)`
  - `AssessmentSubmitted` failed → `masteryScore -= 0.1`
  - Mastery capped at `[0.0, 1.0]`
- [ ] Spaced repetition: SM-2 algorithm updates `NextReviewAt` per topic after each interaction
- [ ] Remedial suggestion triggered when `masteryScore < 0.4` for a topic with available remedial content
- [ ] Collaborative filtering: for new/sparse users, fallback to top-rated courses in their enrolled categories (cold start)
- [ ] Recommendations exclude: already-completed content, content not in user's enrolled courses (unless suggesting new enrollment)
- [ ] `SpacedRepetitionDue` event published daily for users with topics due for review → NotificationWorker sends reminder

### Key data model

```
LearnerMastery { UserId, TenantId, TopicSlug, MasteryScore, RepetitionCount, LastStudiedAt, NextReviewAt, UpdatedAt }
TopicTag { ContentId, ContentType(Course|Lesson), TopicSlug, TenantId }
RecommendationLog { UserId, TenantId, RecommendationType, ContentId, Reasoning, GeneratedAt, ClickedAt? }
```

---

## WP 4.2 — AI Tutor / RAG Chatbot

**Sprint:** 15–16 | **Owner:** AI Team | **Effort:** 3w  
**Service:** `LMS.AiTutorService`

### Context

The AI Tutor is a per-course chatbot powered by Retrieval-Augmented Generation (RAG). It answers questions using only the enrolled course's content as its knowledge base. It operates in Socratic mode by default — asking questions back to guide the student to the answer rather than giving it directly. Instructors can review conversation logs.

### User stories

**US-4.2.1** — As a student, I can ask the AI tutor a question about my course and receive an answer grounded in the course material so that I get accurate, relevant help.

**US-4.2.2** — As a student, the AI tutor asks me guiding questions rather than just giving me the answer so that I develop genuine understanding.

**US-4.2.3** — As a student, the AI tutor only knows about the course I am currently studying so that it doesn't give me information from outside the curriculum.

**US-4.2.4** — As an instructor, I can see the questions students asked the AI tutor so that I understand where students are confused and can improve my content.

**US-4.2.5** — As a student, the AI tutor remembers what I asked earlier in the same session so that conversations are coherent.

### Acceptance criteria

- [ ] `POST /api/tutor/courses/{courseId}/sessions` — starts a new tutor session for an enrolled student; returns `{ sessionId }`
- [ ] `POST /api/tutor/sessions/{sessionId}/messages` — sends a message: `{ content }`; returns `{ reply, sourceLessons[] }` streamed via SSE
- [ ] `GET /api/tutor/sessions/{sessionId}/history` — returns full conversation history
- [ ] `GET /api/tutor/courses/{courseId}/insights` — instructor only; returns `{ topQuestions[], confusionTopics[], sessionCount }` aggregated across all students
- [ ] RAG pipeline:
  1. On course publish: chunk lesson text into 500-token segments → generate embeddings → store in pgvector with `course_id` metadata
  2. On student question: embed query → retrieve top-5 chunks from `course_id` namespace → inject into system prompt
  3. System prompt enforces: answer only from retrieved chunks; use Socratic mode; cite lesson title as source
- [ ] Socratic mode: system prompt instructs model to respond with 1–2 guiding questions before providing direct answer, unless student explicitly asks for a direct explanation after 2 exchanges on the same question
- [ ] Session context: last 10 messages included in each API call (sliding window)
- [ ] Responses streamed to client via Server-Sent Events (SSE) for perceived responsiveness
- [ ] If no relevant content found in vector search: tutor responds "I couldn't find information about that in this course's material. Could you rephrase or check with your instructor?"
- [ ] Conversation stored in `TutorSession` and `TutorMessage` tables for instructor review and analytics
- [ ] Content re-indexed automatically on `CoursePublished` event (incremental: only changed lessons re-embedded)
- [ ] PII guardrail: student's other courses, personal data, grades are never included in context sent to LLM

### Key data model

```
TutorSession { Id, UserId, CourseId, TenantId, StartedAt, LastMessageAt, MessageCount }
TutorMessage { Id, SessionId, Role(user|assistant), Content, SourceLessons[], TokensUsed, CreatedAt }
ContentEmbedding { Id, CourseId, LessonId, TenantId, ChunkIndex, ChunkText, Embedding(vector), CreatedAt }
```

---

## WP 9.3 — SCORM / xAPI / cmi5 Compliance

**Sprint:** 9–10 | **Owner:** Backend | **Effort:** 2w  
**Service:** `LMS.ContentService` + `LMS.ProgressService` (adapter layer)

### Context

Enterprise customers often have existing SCORM or cmi5 content packages. This work package adds the ability to upload SCORM 2004 and cmi5 packages and track completion via the standard protocol, storing results as xAPI statements in ProgressService.

### User stories

**US-9.3.1** — As an instructor, I can upload a SCORM 2004 or cmi5 package and it is playable in the LMS without any conversion so that I can reuse existing content.

**US-9.3.2** — As a student, my completion and score data from a SCORM module is recorded in my progress just like any other lesson so that it counts toward course completion.

**US-9.3.3** — As an admin, all xAPI statements from SCORM and cmi5 modules are stored in our LRS endpoint so that they can be exported to external analytics tools.

### Acceptance criteria

- [ ] SCORM 2004 package upload: extract ZIP → serve `imsmanifest.xml` → launch SCO in sandboxed iframe
- [ ] SCORM API bridge (`API_1484_11` JavaScript object) injected into iframe; captures `SetValue`, `GetValue`, `Commit` calls
- [ ] SCORM completion data (`cmi.completion_status`, `cmi.score.raw`) translated to xAPI statement and published to ProgressService
- [ ] cmi5 packages: serve AU (Assignable Unit) launch URL with `actor`, `registration`, `activityId` parameters; receive xAPI statements at `POST /xapi/statements`
- [ ] LRS endpoint: `POST /xapi/statements` — stores raw xAPI statement in `XApiStatement` table; validates against xAPI 1.0.3 spec
- [ ] `GET /xapi/statements` — admin only; returns statements filtered by `actor`, `verb`, `activity`; compatible with standard LRS query parameters
- [ ] SCORM session state (`cmi.*` data model) persisted in Redis during session; flushed to PostgreSQL on `Terminate`
- [ ] H5P content playback supported: render H5P iframe; capture `xAPI` events emitted by H5P framework

---

## Open Design Decisions

| ID | Decision | Blocks | Options | Recommendation |
|---|---|---|---|---|
| OD-5.1.a | Leaderboard scope: show cross-tenant global ranking? | WP 5.1 | Global leaderboard / Tenant-scoped only | Tenant-scoped default; global opt-in per tenant (privacy concern) |
| OD-5.2.a | Streak timezone: UTC or user local timezone? | WP 5.2 | UTC midnight / User local midnight | User local timezone (stored in IdentityService profile) |
| OD-5.5.a | Badge icons: hosted in ContentService or external CDN? | WP 5.5 | ContentService / Separate static CDN | Separate static CDN (badges are public, no auth needed) |
| OD-6.1.a | Default live session provider: Zoom or Google Meet? | WP 6.1 | Zoom only / Google Meet only / Both configurable | Both configurable per tenant; Zoom as default |
| OD-6.3.a | Forum moderation: automated AI pre-moderation? | WP 6.3 | Instructor-only moderation / AI pre-screening | AI pre-screening deferred to Phase 3; instructor-only for Phase 2 |
| OD-7.1.a | Subscription model: per-user or per-seat (org billing)? | WP 7.1 | Per-user only / Org bulk seat purchase | Per-user for Phase 2; org bulk billing deferred to Phase 3 |
| OD-8.1.a | Analytics store: ClickHouse vs TimescaleDB vs PostgreSQL? | WP 8.1 | ClickHouse / TimescaleDB / PostgreSQL partitioned | ClickHouse for columnar performance at scale; TimescaleDB if team unfamiliar with ClickHouse |
| OD-3.3.a | LLM provider: Anthropic Claude vs OpenAI GPT-4 vs configurable? | WP 3.3, 4.2 | Single provider / Configurable per tenant | Configurable per tenant with Anthropic Claude as default |
| OD-3.4.a | External plagiarism provider: Copyleaks vs Turnitin? | WP 3.4 | Copyleaks / Turnitin / Both | Copyleaks (lower cost, REST API); Turnitin as enterprise option |
| OD-4.2.a | RAG chunk size: 500 tokens vs 1000 tokens? | WP 4.2 | 500 tokens / 1000 tokens / Sentence-level | 500 tokens with 50-token overlap; evaluate recall in Sprint 15 spike |
| OD-4.2.b | AI Tutor: always Socratic or student can toggle to direct mode? | WP 4.2 | Always Socratic / Student toggleable | Student can toggle after 2 Socratic exchanges; default Socratic |
| OD-9.3.a | SCORM API iframe security: same-origin or sandboxed? | WP 9.3 | Same-origin (full API) / Sandboxed iframe | Sandboxed iframe with `allow-scripts allow-same-origin`; postMessage bridge |

---

## Phase 2 Event Contract Summary

| Event | Publisher | Phase 2 Consumers |
|---|---|---|
| `LessonCompleted` | ProgressService | GamificationService, RecommendationService, AnalyticsWorker |
| `CourseCompleted` | ProgressService | GamificationService, RecommendationService, AnalyticsWorker |
| `AssessmentSubmitted` | AssessmentService | GamificationService, RecommendationService, AnalyticsWorker, PlagiarismWorker (if essay) |
| `EssaySubmitted` | AssessmentService | GradingWorker, PlagiarismWorker |
| `LiveSessionAttended` | LiveSessionService | GamificationService, ProgressService, AnalyticsWorker |
| `AchievementUnlocked` | GamificationService | NotificationWorker, AnalyticsWorker |
| `LevelUp` | GamificationService | NotificationWorker |
| `StreakMaintained` | GamificationService | — (XP awarded inline) |
| `StreakBroken` | GamificationService | NotificationWorker |
| `TaskCompleted` | GamificationService | — (XP awarded inline) |
| `GoalCompleted` | GamificationService | NotificationWorker, AnalyticsWorker |
| `PaymentProcessed` | PaymentService | EnrollmentService, NotificationWorker, AnalyticsWorker |
| `PaymentFailed` | PaymentService | NotificationWorker |
| `SubscriptionCancelled` | PaymentService | EnrollmentService, NotificationWorker |
| `PlagiarismFlagged` | PlagiarismWorker | NotificationWorker |
| `SpacedRepetitionDue` | RecommendationService | NotificationWorker |
| `GradeReleased` | GradingWorker | NotificationWorker |

---

## Phase 2 API Surface Summary

| Service | Base path | Auth model | Notes |
|---|---|---|---|
| GamificationService | `/api/gamification` | JWT; student sees own data | SignalR hub at `/hubs/gamification` |
| LiveSessionService | `/api/live-sessions` | JWT; instructor-gated writes | Webhook at `/api/live-sessions/webhooks/zoom` |
| ForumService | `/api/forums` | JWT; enrollment required | Instructor moderation endpoints |
| PaymentService | `/api/payments` | JWT; admin for refunds | Stripe webhook at `/api/payments/webhooks/stripe` |
| AnalyticsWorker | `/api/analytics` | JWT; role-gated | Instructor sees own courses; admin sees all |
| AssessmentService (ext) | `/api/assessments/grading` | JWT; instructor-only | Grade review queue |
| PlagiarismWorker | `/api/plagiarism` | JWT; instructor-only | Webhook at `/api/plagiarism/webhooks/copyleaks` |
| RecommendationService | `/api/recommendations` | JWT; student sees own data | — |
| AiTutorService | `/api/tutor` | JWT; enrollment required | SSE streaming for chat responses |

---

*Document: LMS Phase 2 Feature Specification · Version 1.0 · April 2026*  
*Next: Phase 3 Feature Specification (Marketplace, Skills, Peer Review, GDPR, White-label, DRM, Predictive AI, Offline PWA, Accessibility)*
