# LMS Platform — Phase 2 OpenAPI Specifications

**Phase:** 2 — Learning Intelligence
**OpenAPI version:** 3.1.0
**Services:** Gamification · Live Sessions · Forums · Payment · Analytics · Assessment extensions · Plagiarism · Recommendation · AI Tutor · SCORM

---

## Table of Contents

1. [Gamification Service](#1-gamification-service)
2. [Live Session Service](#2-live-session-service)
3. [Forum Service](#3-forum-service)
4. [Payment Service](#4-payment-service)
5. [Analytics Service](#5-analytics-service)
6. [Assessment Service — Phase 2 Extensions](#6-assessment-service--phase-2-extensions)
7. [Plagiarism Worker](#7-plagiarism-worker)
8. [Recommendation Service](#8-recommendation-service)
9. [AI Tutor Service](#9-ai-tutor-service)

---

## 1. Gamification Service

**Base URL:** `/api/gamification` | **Port:** 5201 | **DB:** PostgreSQL + Redis
**SignalR Hub:** `/hubs/gamification` (JWT via `?access_token=`)

```yaml
openapi: 3.1.0
info:
  title: LMS Gamification Service
  version: 1.0.0
  description: >
    Pure event-consumer service. Manages XP, levels, streaks, tasks,
    goals, badges, and leaderboards. SignalR hub pushes real-time events
    to connected students. Leaderboards stored in Redis sorted sets.

servers:
  - url: /api/gamification

tags:
  - name: Profile
  - name: Leaderboard
  - name: Streak
  - name: Tasks
  - name: Goals
  - name: Badges

paths:

  /me:
    get:
      tags: [Profile]
      summary: Get full gamification profile for current user
      operationId: getMyGamificationProfile
      responses:
        '200':
          description: Gamification profile
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GamificationProfile' }

  /leaderboard:
    get:
      tags: [Leaderboard]
      summary: Get leaderboard
      operationId: getLeaderboard
      parameters:
        - name: scope
          in: query
          required: true
          schema: { type: string, enum: [course, tenant, global] }
        - name: courseId
          in: query
          schema: { $ref: '#/components/schemas/Uuid' }
          description: Required when scope=course
        - name: limit
          in: query
          schema: { type: integer, default: 50, maximum: 100 }
      responses:
        '200':
          description: Ranked list
          content:
            application/json:
              schema:
                type: object
                properties:
                  scope:    { type: string }
                  entries:
                    type: array
                    items: { $ref: '#/components/schemas/LeaderboardEntry' }
                  myRank:   { type: integer, nullable: true }
                  myXp:     { type: integer, nullable: true }

  /streak:
    get:
      tags: [Streak]
      summary: Get current streak info
      operationId: getStreak
      responses:
        '200':
          description: Streak data
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StreakInfo' }

  /streak/freeze:
    post:
      tags: [Streak]
      summary: Use a freeze token to protect yesterday's streak
      operationId: freezeStreak
      responses:
        '200':
          description: Freeze applied
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StreakInfo' }
        '400':
          description: No freeze tokens or streak broken by more than 1 day

  /tasks:
    get:
      tags: [Tasks]
      summary: Get daily and weekly tasks for current user
      operationId: getTasks
      responses:
        '200':
          description: Task lists
          content:
            application/json:
              schema:
                type: object
                properties:
                  daily:  { type: array, items: { $ref: '#/components/schemas/Task' } }
                  weekly: { type: array, items: { $ref: '#/components/schemas/Task' } }

    post:
      tags: [Tasks]
      summary: Create custom course task (instructor/admin)
      operationId: createTask
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateTaskRequest' }
      responses:
        '201':
          description: Task created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Task' }

  /goals:
    get:
      tags: [Goals]
      summary: Get active and completed goals for current user
      operationId: getGoals
      responses:
        '200':
          description: Goals
          content:
            application/json:
              schema:
                type: object
                properties:
                  active:    { type: array, items: { $ref: '#/components/schemas/Goal' } }
                  completed: { type: array, items: { $ref: '#/components/schemas/Goal' } }

    post:
      tags: [Goals]
      summary: Create personal goal
      operationId: createGoal
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateGoalRequest' }
      responses:
        '201':
          description: Goal created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Goal' }

  /goals/{goalId}:
    delete:
      tags: [Goals]
      summary: Abandon personal goal
      operationId: abandonGoal
      parameters:
        - { name: goalId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Abandoned }
        '403': { description: Cannot abandon instructor-assigned goal }

  /goals/assign:
    post:
      tags: [Goals]
      summary: Assign goal to all students in course/tenant (instructor/admin)
      operationId: assignGoal
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/AssignGoalRequest' }
      responses:
        '201': { description: Goal assigned to all students }

  /badges:
    get:
      tags: [Badges]
      summary: Get all badges with earned status and progress
      operationId: getBadges
      responses:
        '200':
          description: Badge list
          content:
            application/json:
              schema:
                type: object
                properties:
                  earned:   { type: array, items: { $ref: '#/components/schemas/EarnedBadge' } }
                  unearned: { type: array, items: { $ref: '#/components/schemas/UnearnedBadge' } }

    post:
      tags: [Badges]
      summary: Create custom badge (instructor/admin)
      operationId: createBadge
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateBadgeRequest' }
      responses:
        '201':
          description: Badge created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/BadgeDefinition' }

  /badges/me:
    get:
      tags: [Badges]
      summary: Get only earned badges for current user
      operationId: getMyBadges
      responses:
        '200':
          description: Earned badges
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/EarnedBadge' } }

components:
  schemas:
    GamificationProfile:
      type: object
      properties:
        userId:        { $ref: '#/components/schemas/Uuid' }
        totalXp:       { type: integer }
        currentLevel:  { type: integer }
        xpToNextLevel: { type: integer }
        rank:          { type: integer, nullable: true }
        streakDays:    { type: integer }
        freezeTokens:  { type: integer }
        badgeCount:    { type: integer }
        recentEvents:
          type: array
          items:
            type: object
            properties:
              type:        { type: string }
              xpAwarded:   { type: integer }
              description: { type: string }
              occurredAt:  { type: string, format: date-time }

    LeaderboardEntry:
      type: object
      properties:
        rank:        { type: integer }
        userId:      { $ref: '#/components/schemas/Uuid' }
        displayName: { type: string }
        avatarUrl:   { type: string, format: uri, nullable: true }
        totalXp:     { type: integer }
        level:       { type: integer }
        badgeCount:  { type: integer }

    StreakInfo:
      type: object
      properties:
        currentStreak:  { type: integer }
        longestStreak:  { type: integer }
        lastActivityDate: { type: string, format: date }
        freezeTokens:   { type: integer }
        checkInHistory:
          type: array
          maxItems: 30
          items:
            type: object
            properties:
              date:       { type: string, format: date }
              active:     { type: boolean }
              isGraceDay: { type: boolean }

    Task:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        title:         { type: string }
        description:   { type: string }
        cadence:       { type: string, enum: [daily, weekly] }
        xpReward:      { type: integer }
        progress:      { type: integer }
        target:        { type: integer }
        completedAt:   { type: string, format: date-time, nullable: true }
        resetAt:       { type: string, format: date-time }

    CreateTaskRequest:
      type: object
      required: [courseId, title, cadence, targetEventType, targetCount, xpReward]
      properties:
        courseId:        { $ref: '#/components/schemas/Uuid' }
        title:           { type: string, maxLength: 200 }
        description:     { type: string, maxLength: 500 }
        cadence:         { type: string, enum: [daily, weekly] }
        targetEventType: { type: string, enum: [lesson_completed, assessment_passed, live_session_attended] }
        targetCount:     { type: integer, minimum: 1 }
        xpReward:        { type: integer, minimum: 1 }

    Goal:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        title:         { type: string }
        targetType:    { type: string, enum: [courses_completed, lessons_completed, xp_earned, assessments_passed] }
        targetValue:   { type: integer }
        currentValue:  { type: integer }
        percent:       { type: number, format: float }
        deadline:      { type: string, format: date-time }
        daysRemaining: { type: integer }
        status:        { type: string, enum: [active, completed, abandoned, missed] }
        isAssigned:    { type: boolean }
        completedAt:   { type: string, format: date-time, nullable: true }

    CreateGoalRequest:
      type: object
      required: [title, targetType, targetValue, deadline]
      properties:
        title:       { type: string, maxLength: 200 }
        targetType:  { type: string, enum: [courses_completed, lessons_completed, xp_earned, assessments_passed] }
        targetValue: { type: integer, minimum: 1 }
        deadline:    { type: string, format: date-time }

    AssignGoalRequest:
      type: object
      required: [title, targetType, targetValue, deadline]
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid', nullable: true }
        title:       { type: string }
        targetType:  { type: string, enum: [courses_completed, lessons_completed, xp_earned, assessments_passed] }
        targetValue: { type: integer, minimum: 1 }
        deadline:    { type: string, format: date-time }

    BadgeDefinition:
      type: object
      properties:
        id:             { $ref: '#/components/schemas/Uuid' }
        name:           { type: string }
        description:    { type: string }
        iconUrl:        { type: string, format: uri }
        rarity:         { type: string, enum: [common, rare, epic, legendary] }
        isBuiltIn:      { type: boolean }
        conditionType:  { type: string }
        conditionThreshold: { type: integer }

    EarnedBadge:
      allOf:
        - { $ref: '#/components/schemas/BadgeDefinition' }
        - type: object
          properties:
            earnedAt: { type: string, format: date-time }

    UnearnedBadge:
      allOf:
        - { $ref: '#/components/schemas/BadgeDefinition' }
        - type: object
          properties:
            progress:  { type: integer }
            target:    { type: integer }

    CreateBadgeRequest:
      type: object
      required: [name, description, iconUrl, rarity, conditionType, conditionThreshold]
      properties:
        courseId:           { $ref: '#/components/schemas/Uuid', nullable: true }
        name:               { type: string, maxLength: 100 }
        description:        { type: string, maxLength: 500 }
        iconUrl:            { type: string, format: uri }
        rarity:             { type: string, enum: [common, rare, epic, legendary] }
        conditionType:
          type: string
          enum: [courses_completed, streak_days, xp_earned, assessment_perfect,
                 lessons_completed, live_sessions_attended, forum_upvotes]
        conditionThreshold: { type: integer, minimum: 1 }
```

---

## 2. Live Session Service

**Base URL:** `/api/live-sessions` | **Port:** 5202 | **DB:** `lms_livesessions` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Live Session Service
  version: 1.0.0
  description: >
    Manages scheduled live sessions via Zoom or Google Meet.
    Handles scheduling, attendance tracking via webhooks,
    and recording ingestion to ContentService.

servers:
  - url: /api/live-sessions

tags:
  - name: Sessions
  - name: Attendance
  - name: Recordings
  - name: Webhooks

paths:

  /:
    post:
      tags: [Sessions]
      summary: Schedule a live session (instructor/admin)
      operationId: createSession
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateSessionRequest' }
      responses:
        '201':
          description: Session scheduled
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LiveSession' }
        '400': { $ref: '#/components/responses/BadRequest' }

    get:
      tags: [Sessions]
      summary: List sessions for a course
      operationId: listSessions
      parameters:
        - { name: courseId,  in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: status,    in: query, schema: { type: string, enum: [scheduled, live, ended, cancelled] } }
        - { name: from,      in: query, schema: { type: string, format: date-time } }
        - { name: to,        in: query, schema: { type: string, format: date-time } }
      responses:
        '200':
          description: Session list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/LiveSession' } }

  /me/upcoming:
    get:
      tags: [Sessions]
      summary: Upcoming sessions across all enrolled courses for current student
      operationId: getMyUpcomingSessions
      responses:
        '200':
          description: Upcoming sessions
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/LiveSessionSummary' } }

  /{sessionId}:
    get:
      tags: [Sessions]
      summary: Get session detail
      operationId: getSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Session
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LiveSession' }

    put:
      tags: [Sessions]
      summary: Update session time/title (instructor/admin)
      operationId: updateSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateSessionRequest' }
      responses:
        '200':
          description: Updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LiveSession' }

    delete:
      tags: [Sessions]
      summary: Cancel session (instructor/admin) — notifies students
      operationId: cancelSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '204': { description: Cancelled }

  /{sessionId}/attendance:
    get:
      tags: [Attendance]
      summary: Get attendance records (instructor/admin)
      operationId: getAttendance
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Attendance
          content:
            application/json:
              schema:
                type: object
                properties:
                  sessionId:      { $ref: '#/components/schemas/Uuid' }
                  totalAttendees: { type: integer }
                  eligibleCount:  { type: integer }
                  records:
                    type: array
                    items: { $ref: '#/components/schemas/AttendanceRecord' }

  /{sessionId}/recording:
    get:
      tags: [Recordings]
      summary: Get signed recording stream URL (requires enrollment)
      operationId: getRecording
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Recording stream URL
          content:
            application/json:
              schema:
                type: object
                properties:
                  streamUrl:  { type: string, format: uri }
                  expiresAt:  { type: string, format: date-time }
        '404': { description: No recording available }
        '403': { description: Not enrolled or recording not yet published }

  /{sessionId}/recording/publish:
    post:
      tags: [Recordings]
      summary: Publish recording to students (instructor, when autoPublish=false)
      operationId: publishRecording
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200': { description: Recording published }

  /webhooks/zoom:
    post:
      tags: [Webhooks]
      summary: Zoom webhook receiver
      operationId: zoomWebhook
      security: []
      description: >
        Validates X-Zm-Signature header.
        Handles: meeting.started, meeting.ended,
        meeting.participant_joined, meeting.participant_left,
        recording.completed
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/ZoomWebhookPayload' }
      responses:
        '200': { description: Acknowledged }
        '401': { description: Invalid signature }

components:
  parameters:
    sessionId:
      name: sessionId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CreateSessionRequest:
      type: object
      required: [courseId, title, scheduledAt, durationMinutes, provider]
      properties:
        courseId:        { $ref: '#/components/schemas/Uuid' }
        title:           { type: string, maxLength: 200 }
        description:     { type: string, maxLength: 1000 }
        scheduledAt:     { type: string, format: date-time }
        durationMinutes: { type: integer, minimum: 15, maximum: 480 }
        provider:        { type: string, enum: [zoom, google_meet] }
        autoPublishRecording: { type: boolean, default: true }

    UpdateSessionRequest:
      type: object
      properties:
        title:           { type: string, maxLength: 200 }
        description:     { type: string }
        scheduledAt:     { type: string, format: date-time }
        durationMinutes: { type: integer }

    LiveSession:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        courseId:        { $ref: '#/components/schemas/Uuid' }
        tenantId:        { $ref: '#/components/schemas/Uuid' }
        instructorId:    { $ref: '#/components/schemas/Uuid' }
        title:           { type: string }
        description:     { type: string, nullable: true }
        scheduledAt:     { type: string, format: date-time }
        durationMinutes: { type: integer }
        provider:        { type: string, enum: [zoom, google_meet] }
        joinUrl:         { type: string, format: uri }
        hostUrl:         { type: string, format: uri, description: Instructor only }
        status:          { type: string, enum: [scheduled, live, ended, cancelled] }
        recordingStatus: { type: string, enum: [none, pending, processing, published, review_required], nullable: true }
        createdAt:       { type: string, format: date-time }

    LiveSessionSummary:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        courseId:    { $ref: '#/components/schemas/Uuid' }
        courseTitle: { type: string }
        title:       { type: string }
        scheduledAt: { type: string, format: date-time }
        joinUrl:     { type: string, format: uri }
        status:      { type: string }

    AttendanceRecord:
      type: object
      properties:
        userId:          { $ref: '#/components/schemas/Uuid' }
        displayName:     { type: string }
        joinedAt:        { type: string, format: date-time }
        leftAt:          { type: string, format: date-time, nullable: true }
        durationMinutes: { type: integer }
        isEligible:      { type: boolean, description: Attended >= 10 minutes }

    ZoomWebhookPayload:
      type: object
      properties:
        event:   { type: string }
        payload: { type: object }
```

---

## 3. Forum Service

**Base URL:** `/api/forums` | **Port:** 5203 | **DB:** `lms_forums` (PostgreSQL) + Redis

```yaml
openapi: 3.1.0
info:
  title: LMS Forum Service
  version: 1.0.0
  description: >
    Course discussion forums with threaded posts, upvotes,
    pinning, and moderation. Enrollment required to post.

servers:
  - url: /api/forums

tags:
  - name: Posts
  - name: Moderation

paths:

  /courses/{courseId}/posts:
    post:
      tags: [Posts]
      summary: Create a forum post (requires enrollment)
      operationId: createPost
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreatePostRequest' }
      responses:
        '201':
          description: Post created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ForumPost' }
        '403': { description: Not enrolled }

    get:
      tags: [Posts]
      summary: List forum posts for a course
      operationId: listPosts
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: sort, in: query, schema: { type: string, enum: [newest, top, unanswered], default: newest } }
        - { name: page, in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize, in: query, schema: { type: integer, default: 20 } }
      responses:
        '200':
          description: Post list (pinned posts always first)
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/ForumPostSummary' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /posts/{postId}:
    get:
      tags: [Posts]
      summary: Get post with replies
      operationId: getPost
      parameters:
        - { $ref: '#/components/parameters/postId' }
      responses:
        '200':
          description: Post with thread
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ForumPostDetail' }

    delete:
      tags: [Moderation]
      summary: Delete post (author or moderator) — soft delete
      operationId: deletePost
      parameters:
        - { $ref: '#/components/parameters/postId' }
      responses:
        '204': { description: Deleted (content replaced with [deleted]) }

  /posts/{postId}/replies:
    post:
      tags: [Posts]
      summary: Reply to a post (max depth 2 levels)
      operationId: createReply
      parameters:
        - { $ref: '#/components/parameters/postId' }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [body]
              properties:
                body: { type: string, maxLength: 5000 }
      responses:
        '201':
          description: Reply created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ForumPost' }
        '400': { description: Max reply depth exceeded }

  /posts/{postId}/upvote:
    post:
      tags: [Posts]
      summary: Toggle upvote on a post
      operationId: toggleUpvote
      parameters:
        - { $ref: '#/components/parameters/postId' }
      responses:
        '200':
          description: Updated vote count
          content:
            application/json:
              schema:
                type: object
                properties:
                  voteCount: { type: integer }
                  voted:     { type: boolean }

  /posts/{postId}/pin:
    patch:
      tags: [Moderation]
      summary: Toggle pin status (instructor/admin)
      operationId: togglePin
      parameters:
        - { $ref: '#/components/parameters/postId' }
      responses:
        '200':
          description: Pin status updated
          content:
            application/json:
              schema:
                type: object
                properties:
                  isPinned: { type: boolean }

  /posts/{postId}/hide:
    patch:
      tags: [Moderation]
      summary: Hide/unhide post (instructor/admin)
      operationId: toggleHide
      parameters:
        - { $ref: '#/components/parameters/postId' }
      responses:
        '200':
          description: Visibility updated
          content:
            application/json:
              schema:
                type: object
                properties:
                  isHidden: { type: boolean }

components:
  parameters:
    postId:
      name: postId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CreatePostRequest:
      type: object
      required: [body]
      properties:
        title: { type: string, maxLength: 200, nullable: true }
        body:  { type: string, maxLength: 10000, description: Markdown supported }

    ForumPostSummary:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        courseId:    { $ref: '#/components/schemas/Uuid' }
        authorId:    { $ref: '#/components/schemas/Uuid' }
        authorName:  { type: string }
        title:       { type: string, nullable: true }
        bodyPreview: { type: string, maxLength: 200 }
        isPinned:    { type: boolean }
        isHidden:    { type: boolean }
        voteCount:   { type: integer }
        replyCount:  { type: integer }
        createdAt:   { type: string, format: date-time }

    ForumPost:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        courseId:    { $ref: '#/components/schemas/Uuid' }
        authorId:    { $ref: '#/components/schemas/Uuid' }
        authorName:  { type: string }
        title:       { type: string, nullable: true }
        body:        { type: string }
        isPinned:    { type: boolean }
        isHidden:    { type: boolean }
        voteCount:   { type: integer }
        parentPostId:{ $ref: '#/components/schemas/Uuid', nullable: true }
        createdAt:   { type: string, format: date-time }
        updatedAt:   { type: string, format: date-time }

    ForumPostDetail:
      allOf:
        - { $ref: '#/components/schemas/ForumPost' }
        - type: object
          properties:
            replies:
              type: array
              items: { $ref: '#/components/schemas/ForumPost' }
```

---

## 4. Payment Service

**Base URL:** `/api/payments` | **Port:** 5204 | **DB:** `lms_payments` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Payment Service
  version: 1.0.0
  description: >
    Stripe-powered payments for course purchases and subscriptions.
    No card data ever touches LMS servers — all payment data stays in Stripe.
    Phase 3 extends with Stripe Connect revenue sharing.

servers:
  - url: /api/payments

tags:
  - name: Checkout
  - name: Subscriptions
  - name: Invoices
  - name: Webhooks
  - name: Admin

paths:

  /checkout:
    post:
      tags: [Checkout]
      summary: Create Stripe checkout session
      operationId: createCheckout
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CheckoutRequest' }
      responses:
        '200':
          description: Stripe checkout URL
          content:
            application/json:
              schema:
                type: object
                properties:
                  checkoutUrl: { type: string, format: uri }
                  sessionId:   { type: string }

  /subscriptions/me:
    get:
      tags: [Subscriptions]
      summary: Get current user subscription status
      operationId: getMySubscription
      responses:
        '200':
          description: Subscription status
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Subscription' }
        '404': { description: No active subscription }

  /subscriptions/cancel:
    post:
      tags: [Subscriptions]
      summary: Cancel subscription at period end
      operationId: cancelSubscription
      responses:
        '200':
          description: Subscription will cancel at period end
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Subscription' }

  /invoices/me:
    get:
      tags: [Invoices]
      summary: Get invoice history for current user
      operationId: getMyInvoices
      parameters:
        - { name: page,     in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize, in: query, schema: { type: integer, default: 20 } }
      responses:
        '200':
          description: Invoice list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/Invoice' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /refunds:
    post:
      tags: [Admin]
      summary: Issue refund (admin only)
      operationId: issueRefund
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [purchaseId]
              properties:
                purchaseId: { $ref: '#/components/schemas/Uuid' }
                reason:     { type: string }
      responses:
        '200': { description: Refund issued }
        '400': { description: Outside 14-day refund window }
        '403': { $ref: '#/components/responses/Forbidden' }

  /coupons/validate:
    post:
      tags: [Checkout]
      summary: Validate coupon code before checkout
      operationId: validateCoupon
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [code, courseId]
              properties:
                code:     { type: string }
                courseId: { $ref: '#/components/schemas/Uuid' }
      responses:
        '200':
          description: Validation result
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CouponValidationResult' }

  /webhooks/stripe:
    post:
      tags: [Webhooks]
      summary: Stripe webhook receiver
      operationId: stripeWebhook
      security: []
      description: >
        Validates Stripe-Signature header with STRIPE_WEBHOOK_SECRET.
        Handles: checkout.session.completed, invoice.payment_failed,
        customer.subscription.deleted, charge.refunded
      requestBody:
        required: true
        content:
          application/json:
            schema: { type: object }
      responses:
        '200': { description: Acknowledged }
        '400': { description: Invalid signature }

  # Phase 3 stubs (Stripe Connect)
  /connect/onboard:
    post:
      tags: [Admin]
      summary: Initiate Stripe Connect instructor onboarding (Phase 3)
      operationId: connectOnboard
      responses:
        '200':
          description: Onboarding URL
          content:
            application/json:
              schema:
                type: object
                properties:
                  onboardingUrl: { type: string, format: uri }

  /earnings:
    get:
      tags: [Admin]
      summary: Get instructor earnings summary (Phase 3)
      operationId: getEarnings
      responses:
        '200':
          description: Earnings
          content:
            application/json:
              schema: { $ref: '#/components/schemas/EarningsSummary' }

components:
  schemas:
    CheckoutRequest:
      type: object
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid', nullable: true }
        planId:      { type: string, nullable: true, description: Stripe Price ID for subscription }
        couponCode:  { type: string, nullable: true }
      description: Provide either courseId (one-time) or planId (subscription)

    Subscription:
      type: object
      properties:
        id:                { $ref: '#/components/schemas/Uuid' }
        stripeSubId:       { type: string }
        planId:            { type: string }
        status:            { type: string, enum: [active, past_due, cancelled, trialing] }
        currentPeriodEnd:  { type: string, format: date-time }
        cancelAtPeriodEnd: { type: boolean }

    Invoice:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        amount:      { type: integer, description: Amount in cents }
        currency:    { type: string, example: usd }
        status:      { type: string, enum: [paid, open, void, uncollectible] }
        description: { type: string }
        invoiceUrl:  { type: string, format: uri }
        pdfUrl:      { type: string, format: uri }
        createdAt:   { type: string, format: date-time }

    CouponValidationResult:
      type: object
      properties:
        valid:         { type: boolean }
        discountType:  { type: string, enum: [percent, fixed], nullable: true }
        discountValue: { type: number, nullable: true }
        originalPrice: { type: integer, nullable: true }
        finalPrice:    { type: integer, nullable: true }
        error:         { type: string, nullable: true }

    EarningsSummary:
      type: object
      properties:
        totalEarned:       { type: integer }
        pendingPayout:     { type: integer }
        lastPayoutAmount:  { type: integer }
        lastPayoutDate:    { type: string, format: date-time, nullable: true }
        currency:          { type: string }
```

---

## 5. Analytics Service

**Base URL:** `/api/analytics` | **Port:** 5205 | **DB:** ClickHouse

```yaml
openapi: 3.1.0
info:
  title: LMS Analytics Service
  version: 1.0.0
  description: >
    Read-only analytics API backed by ClickHouse CQRS projections.
    Never queries transactional databases. All data < 15 min old.
    Response data cached in Redis for 15 minutes.

servers:
  - url: /api/analytics

tags:
  - name: Instructor
  - name: Platform
  - name: AtRisk
    description: Phase 3 predictive at-risk features (stubs)

paths:

  /instructor/courses/{courseId}/overview:
    get:
      tags: [Instructor]
      summary: Course overview metrics for instructor
      operationId: getCourseOverview
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Overview
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseOverview' }

  /instructor/courses/{courseId}/funnel:
    get:
      tags: [Instructor]
      summary: Lesson completion funnel (drop-off analysis)
      operationId: getCourseFunnel
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Funnel data
          content:
            application/json:
              schema:
                type: object
                properties:
                  lessons:
                    type: array
                    items: { $ref: '#/components/schemas/FunnelStep' }

  /instructor/courses/{courseId}/lessons/{lessonId}:
    get:
      tags: [Instructor]
      summary: Per-lesson engagement metrics
      operationId: getLessonMetrics
      parameters:
        - { name: courseId,  in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: lessonId,  in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Lesson metrics
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LessonMetrics' }

  /instructor/courses/{courseId}/assessments/{assessmentId}:
    get:
      tags: [Instructor]
      summary: Assessment question-level analytics
      operationId: getAssessmentMetrics
      parameters:
        - { name: courseId,      in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: assessmentId,  in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Assessment analytics
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AssessmentMetrics' }

  /instructor/courses/{courseId}/students/inactive:
    get:
      tags: [Instructor]
      summary: Students inactive for N days
      operationId: getInactiveStudents
      parameters:
        - { name: courseId,  in: path,  required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: days,      in: query, schema: { type: integer, default: 7, minimum: 1 } }
      responses:
        '200':
          description: Inactive students
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/InactiveStudent' }

  /instructor/courses/{courseId}/enrollments/timeseries:
    get:
      tags: [Instructor]
      summary: Daily enrollment count time series
      operationId: getEnrollmentTimeseries
      parameters:
        - { name: courseId, in: path,  required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: from,     in: query, schema: { type: string, format: date } }
        - { name: to,       in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Time series
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      type: object
                      properties:
                        date:  { type: string, format: date }
                        count: { type: integer }

  /platform:
    get:
      tags: [Platform]
      summary: Platform-wide metrics (admin only)
      operationId: getPlatformMetrics
      parameters:
        - { name: from, in: query, schema: { type: string, format: date } }
        - { name: to,   in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Platform metrics
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PlatformMetrics' }

  /instructor/courses/{courseId}/at-risk:
    get:
      tags: [AtRisk]
      summary: At-risk students in a course (Phase 3)
      operationId: getAtRiskStudents
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: At-risk list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/AtRiskStudent' }

  /at-risk/{enrollmentId}/dismiss:
    post:
      tags: [AtRisk]
      summary: Dismiss at-risk flag for 7 days (Phase 3)
      operationId: dismissAtRisk
      parameters:
        - { name: enrollmentId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Dismissed }

components:
  schemas:
    CourseOverview:
      type: object
      properties:
        courseId:         { $ref: '#/components/schemas/Uuid' }
        totalEnrolled:    { type: integer }
        completionRate:   { type: number, format: float }
        avgScore:         { type: number, format: float }
        avgWatchPercent:  { type: number, format: float }
        activeLastWeek:   { type: integer }
        dropOffLessonId:  { $ref: '#/components/schemas/Uuid', nullable: true }
        dropOffLessonTitle:{ type: string, nullable: true }

    FunnelStep:
      type: object
      properties:
        lessonId:       { $ref: '#/components/schemas/Uuid' }
        lessonTitle:    { type: string }
        order:          { type: integer }
        startedCount:   { type: integer }
        completedCount: { type: integer }
        dropOffRate:    { type: number, format: float }

    LessonMetrics:
      type: object
      properties:
        lessonId:           { $ref: '#/components/schemas/Uuid' }
        avgWatchPercent:    { type: number, format: float }
        completionRate:     { type: number, format: float }
        avgTimeSpentSeconds:{ type: integer }
        replayRate:         { type: number, format: float }

    AssessmentMetrics:
      type: object
      properties:
        assessmentId:  { $ref: '#/components/schemas/Uuid' }
        totalAttempts: { type: integer }
        passRate:      { type: number, format: float }
        avgScore:      { type: number, format: float }
        questions:
          type: array
          items:
            type: object
            properties:
              questionId:                   { $ref: '#/components/schemas/Uuid' }
              prompt:                       { type: string }
              correctRate:                  { type: number, format: float }
              mostSelectedWrongOptionIndex: { type: integer, nullable: true }

    InactiveStudent:
      type: object
      properties:
        userId:        { $ref: '#/components/schemas/Uuid' }
        displayName:   { type: string }
        lastActiveAt:  { type: string, format: date-time }
        inactiveDays:  { type: integer }
        completionPct: { type: number, format: float }

    PlatformMetrics:
      type: object
      properties:
        dau:              { type: integer }
        wau:              { type: integer }
        mau:              { type: integer }
        newEnrollments:   { type: integer }
        courseCompletions:{ type: integer }
        revenue:          { type: integer, description: Cents }
        currency:         { type: string }

    AtRiskStudent:
      type: object
      properties:
        userId:        { $ref: '#/components/schemas/Uuid' }
        displayName:   { type: string }
        enrollmentId:  { $ref: '#/components/schemas/Uuid' }
        riskScore:     { type: number, format: float }
        riskFactors:
          type: array
          items:
            type: object
            properties:
              factor:       { type: string }
              contribution: { type: number, format: float }
        suggestedAction: { type: string }
        lastActiveAt:   { type: string, format: date-time }
        scoredAt:       { type: string, format: date-time }
```

---

## 6. Assessment Service — Phase 2 Extensions

These endpoints extend the existing Phase 1 Assessment Service (port 5106).

```yaml
# Adaptive assessment extensions
paths:

  /{assessmentId}/analytics/ability-distribution:
    get:
      tags: [Analytics]
      summary: Ability (theta) distribution across all adaptive attempts
      operationId: getAbilityDistribution
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      responses:
        '200':
          description: Histogram of ability estimates
          content:
            application/json:
              schema:
                type: object
                properties:
                  assessmentId: { $ref: '#/components/schemas/Uuid' }
                  buckets:
                    type: array
                    items:
                      type: object
                      properties:
                        rangeMin:  { type: number, format: float }
                        rangeMax:  { type: number, format: float }
                        count:     { type: integer }
                  avgTheta:  { type: number, format: float }
                  medianTheta: { type: number, format: float }

  /attempts/{attemptId}/feedback:
    get:
      tags: [Sessions]
      summary: Get AI-generated feedback for essay attempt (student)
      operationId: getAttemptFeedback
      description: Only available after grade status = released
      parameters:
        - { name: attemptId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Feedback
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AttemptFeedback' }
        '404': { description: Not found or not yet released }

# Grade appeal extensions
  /appeals:
    get:
      tags: [Grading]
      summary: List appeals (instructor = own courses; admin = all)
      operationId: listAppeals
      parameters:
        - { name: status, in: query, schema: { type: string, enum: [submitted, instructor_review, escalated, resolved] } }
        - { name: page,   in: query, schema: { type: integer, default: 1 } }
      responses:
        '200':
          description: Appeals
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/Appeal' } }

  /appeals/{appealId}:
    get:
      tags: [Grading]
      summary: Get appeal with full event log
      operationId: getAppeal
      parameters:
        - { name: appealId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Appeal detail
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AppealDetail' }

  /appeals/{appealId}/respond:
    post:
      tags: [Grading]
      summary: Instructor responds to appeal (uphold or revise grade)
      operationId: respondToAppeal
      parameters:
        - { name: appealId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [decision]
              properties:
                decision:     { type: string, enum: [uphold, revise] }
                revisedScore: { type: number, format: float, nullable: true }
                responseText: { type: string, maxLength: 2000 }
      responses:
        '200': { description: Response recorded }

  /appeals/{appealId}/escalate:
    post:
      tags: [Grading]
      summary: Student escalates appeal to admin
      operationId: escalateAppeal
      parameters:
        - { name: appealId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [escalationReason]
              properties:
                escalationReason: { type: string, minLength: 10, maxLength: 2000 }
      responses:
        '200': { description: Escalated }

  /appeals/{appealId}/resolve:
    post:
      tags: [Grading]
      summary: Admin makes final resolution
      operationId: resolveAppeal
      parameters:
        - { name: appealId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [decision]
              properties:
                decision:     { type: string, enum: [uphold, revise] }
                revisedScore: { type: number, format: float, nullable: true }
                notes:        { type: string }
      responses:
        '200': { description: Resolved }

components:
  schemas:
    AttemptFeedback:
      type: object
      properties:
        attemptId:        { $ref: '#/components/schemas/Uuid' }
        finalScore:       { type: number, format: float }
        maxScore:         { type: number, format: float }
        feedbackText:     { type: string }
        criteriaScores:
          type: array
          items:
            type: object
            properties:
              criterionName: { type: string }
              score:         { type: number, format: float }
              maxPoints:     { type: integer }
        gradedBy:         { type: string, enum: [ai, instructor] }
        releasedAt:       { type: string, format: date-time }

    Appeal:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        attemptId:     { $ref: '#/components/schemas/Uuid' }
        studentId:     { $ref: '#/components/schemas/Uuid' }
        studentName:   { type: string }
        courseTitle:   { type: string }
        status:        { type: string, enum: [submitted, instructor_review, escalated, resolved] }
        submittedAt:   { type: string, format: date-time }
        resolvedAt:    { type: string, format: date-time, nullable: true }

    AppealDetail:
      allOf:
        - { $ref: '#/components/schemas/Appeal' }
        - type: object
          properties:
            justification: { type: string }
            events:
              type: array
              items:
                type: object
                properties:
                  actorId:    { $ref: '#/components/schemas/Uuid' }
                  actorRole:  { type: string }
                  action:     { type: string }
                  notes:      { type: string }
                  occurredAt: { type: string, format: date-time }
```

---

## 7. Plagiarism Worker

**Base URL:** `/api/plagiarism` | **Port:** 5206 | **DB:** PostgreSQL + pgvector

```yaml
openapi: 3.1.0
info:
  title: LMS Plagiarism Worker
  version: 1.0.0
  description: >
    Runs internal similarity checks (pgvector cosine similarity) and
    optional external checks (Copyleaks) on essay submissions.
    Instructors review flagged submissions; students never directly notified.

servers:
  - url: /api/plagiarism

tags:
  - name: Reports
  - name: Config
  - name: Webhooks

paths:

  /reports/{submissionId}:
    get:
      tags: [Reports]
      summary: Get plagiarism report for a submission (instructor/admin)
      operationId: getPlagiarismReport
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Report
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PlagiarismReport' }
        '404': { $ref: '#/components/responses/NotFound' }

  /reports/{submissionId}/dismiss:
    post:
      tags: [Reports]
      summary: Dismiss plagiarism flag (instructor)
      operationId: dismissFlag
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [reason]
              properties:
                reason: { type: string, maxLength: 1000 }
      responses:
        '204': { description: Dismissed }

  /reports/{submissionId}/escalate:
    post:
      tags: [Reports]
      summary: Escalate to academic integrity case (instructor)
      operationId: escalateFlag
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '201': { description: Case created }

  /config:
    post:
      tags: [Config]
      summary: Set plagiarism thresholds for a course (instructor)
      operationId: setConfig
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/PlagiarismConfig' }
      responses:
        '200':
          description: Config saved
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PlagiarismConfig' }

  /webhooks/copyleaks:
    post:
      tags: [Webhooks]
      summary: Copyleaks external check callback
      operationId: copyleaksWebhook
      security: []
      description: Receives similarity score from Copyleaks after external scan completes.
      requestBody:
        required: true
        content:
          application/json:
            schema: { type: object }
      responses:
        '200': { description: Acknowledged }

components:
  schemas:
    PlagiarismReport:
      type: object
      properties:
        submissionId:           { $ref: '#/components/schemas/Uuid' }
        studentId:              { $ref: '#/components/schemas/Uuid' }
        internalSimilarityScore:{ type: number, format: float }
        externalSimilarityScore:{ type: number, format: float, nullable: true }
        flagLevel:              { type: string, enum: [clear, warning, flag] }
        status:                 { type: string, enum: [pending, flagged, dismissed, escalated] }
        matchedSubmissions:
          type: array
          items:
            type: object
            properties:
              submissionId: { $ref: '#/components/schemas/Uuid' }
              similarity:   { type: number, format: float }
              courseTitle:  { type: string }
              submittedAt:  { type: string, format: date-time }
        createdAt: { type: string, format: date-time }

    PlagiarismConfig:
      type: object
      required: [courseId, warningThreshold, flagThreshold]
      properties:
        courseId:           { $ref: '#/components/schemas/Uuid' }
        warningThreshold:   { type: number, format: float, default: 20 }
        flagThreshold:      { type: number, format: float, default: 40 }
        externalEnabled:    { type: boolean, default: false }
```

---

## 8. Recommendation Service

**Base URL:** `/api/recommendations` | **Port:** 5207 | **DB:** PostgreSQL + pgvector

```yaml
openapi: 3.1.0
info:
  title: LMS Recommendation Service
  version: 1.0.0
  description: >
    Personalized learning path recommendations based on mastery scores,
    knowledge graph, and spaced repetition. Pure consumer — no outbound
    HTTP calls to other services at query time.

servers:
  - url: /api/recommendations

tags:
  - name: Recommendations
  - name: Mastery

paths:

  /me:
    get:
      tags: [Recommendations]
      summary: Get personalized recommendations for current user
      operationId: getMyRecommendations
      responses:
        '200':
          description: Recommendations
          content:
            application/json:
              schema: { $ref: '#/components/schemas/RecommendationSet' }

  /me/path:
    get:
      tags: [Recommendations]
      summary: Get ordered next-step learning path with reasoning
      operationId: getMyPath
      responses:
        '200':
          description: Learning path
          content:
            application/json:
              schema:
                type: object
                properties:
                  steps:
                    type: array
                    items: { $ref: '#/components/schemas/PathStep' }

  /me/mastery:
    get:
      tags: [Mastery]
      summary: Get topic mastery scores for current user
      operationId: getMyMastery
      parameters:
        - { name: minMastery, in: query, schema: { type: number, format: float, default: 0 } }
        - { name: maxMastery, in: query, schema: { type: number, format: float, default: 1 } }
      responses:
        '200':
          description: Mastery scores
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/MasteryScore' }

  /me/gaps:
    get:
      tags: [Mastery]
      summary: Get skill gaps for current user (used by SkillsService)
      operationId: getMyGaps
      responses:
        '200':
          description: Skill gaps
          content:
            application/json:
              schema:
                type: object
                properties:
                  gaps:
                    type: array
                    items:
                      type: object
                      properties:
                        topicSlug:       { type: string }
                        masteryScore:    { type: number, format: float }
                        recommendedCourses:
                          type: array
                          items: { $ref: '#/components/schemas/Uuid' }

components:
  schemas:
    RecommendationSet:
      type: object
      properties:
        userId:          { $ref: '#/components/schemas/Uuid' }
        nextLesson:
          nullable: true
          type: object
          properties:
            lessonId:    { $ref: '#/components/schemas/Uuid' }
            lessonTitle: { type: string }
            courseTitle: { type: string }
            reason:      { type: string }
        recommendedCourses:
          type: array
          items: { $ref: '#/components/schemas/RecommendedCourse' }
        remedialSuggestions:
          type: array
          items: { $ref: '#/components/schemas/RecommendedCourse' }
        spacedRepetitionItems:
          type: array
          items: { $ref: '#/components/schemas/SpacedRepetitionItem' }
        generatedAt: { type: string, format: date-time }

    RecommendedCourse:
      type: object
      properties:
        courseId:       { $ref: '#/components/schemas/Uuid' }
        courseTitle:    { type: string }
        thumbnailUrl:   { type: string, format: uri, nullable: true }
        reason:         { type: string }
        matchScore:     { type: number, format: float }

    SpacedRepetitionItem:
      type: object
      properties:
        topicSlug:    { type: string }
        topicName:    { type: string }
        masteryScore: { type: number, format: float }
        nextReviewAt: { type: string, format: date-time }
        suggestedLesson:
          nullable: true
          type: object
          properties:
            lessonId:  { $ref: '#/components/schemas/Uuid' }
            lessonTitle:{ type: string }

    PathStep:
      type: object
      properties:
        order:       { type: integer }
        type:        { type: string, enum: [lesson, course, review] }
        contentId:   { $ref: '#/components/schemas/Uuid' }
        title:       { type: string }
        reason:      { type: string }
        priority:    { type: string, enum: [critical, recommended, optional] }

    MasteryScore:
      type: object
      properties:
        topicSlug:       { type: string }
        topicName:       { type: string }
        masteryScore:    { type: number, format: float }
        repetitionCount: { type: integer }
        lastStudiedAt:   { type: string, format: date-time }
        nextReviewAt:    { type: string, format: date-time }
```

---

## 9. AI Tutor Service

**Base URL:** `/api/tutor` | **Port:** 5208 | **DB:** PostgreSQL + pgvector

```yaml
openapi: 3.1.0
info:
  title: LMS AI Tutor Service
  version: 1.0.0
  description: >
    Per-course RAG chatbot powered by Semantic Kernel.
    Answers only from enrolled course material (pgvector per-course namespace).
    Operates in Socratic mode by default. Responses streamed via SSE.
    Enrollment required. PII from other services never included in LLM context.

servers:
  - url: /api/tutor

tags:
  - name: Sessions
  - name: Insights

paths:

  /courses/{courseId}/sessions:
    post:
      tags: [Sessions]
      summary: Start a new tutor session (requires enrollment)
      operationId: startTutorSession
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '201':
          description: Session started
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TutorSession' }
        '403': { description: Not enrolled in course }

  /sessions/{sessionId}/messages:
    post:
      tags: [Sessions]
      summary: Send a message to the AI tutor (SSE streamed response)
      operationId: sendMessage
      description: >
        Response is streamed via Server-Sent Events (SSE).
        Client must set Accept: text/event-stream.
        Each SSE event is a JSON chunk: { delta: string, done: boolean }.
        Final event includes sourceLessons[].
      parameters:
        - { name: sessionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [content]
              properties:
                content:   { type: string, maxLength: 2000 }
                directMode:{ type: boolean, default: false, description: Skip Socratic mode for this message }
      responses:
        '200':
          description: SSE stream
          content:
            text/event-stream:
              schema:
                type: string
                description: |
                  SSE stream format:
                  data: {"delta": "Here is", "done": false}
                  data: {"delta": " a hint...", "done": false}
                  data: {"delta": "", "done": true, "sourceLessons": [...]}
        '404': { description: Session not found }
        '403': { description: Enrollment expired }

  /sessions/{sessionId}/history:
    get:
      tags: [Sessions]
      summary: Get full conversation history
      operationId: getSessionHistory
      parameters:
        - { name: sessionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: History
          content:
            application/json:
              schema:
                type: object
                properties:
                  session:  { $ref: '#/components/schemas/TutorSession' }
                  messages: { type: array, items: { $ref: '#/components/schemas/TutorMessage' } }

  /courses/{courseId}/insights:
    get:
      tags: [Insights]
      summary: AI tutor usage insights for instructor
      operationId: getTutorInsights
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Insights
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TutorInsights' }

components:
  schemas:
    TutorSession:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        courseId:      { $ref: '#/components/schemas/Uuid' }
        userId:        { $ref: '#/components/schemas/Uuid' }
        messageCount:  { type: integer }
        startedAt:     { type: string, format: date-time }
        lastMessageAt: { type: string, format: date-time, nullable: true }

    TutorMessage:
      type: object
      properties:
        id:           { $ref: '#/components/schemas/Uuid' }
        sessionId:    { $ref: '#/components/schemas/Uuid' }
        role:         { type: string, enum: [user, assistant] }
        content:      { type: string }
        sourceLessons:
          type: array
          nullable: true
          items:
            type: object
            properties:
              lessonId:    { $ref: '#/components/schemas/Uuid' }
              lessonTitle: { type: string }
        tokensUsed:   { type: integer, nullable: true }
        createdAt:    { type: string, format: date-time }

    TutorInsights:
      type: object
      properties:
        courseId:       { $ref: '#/components/schemas/Uuid' }
        sessionCount:   { type: integer }
        totalMessages:  { type: integer }
        topQuestions:
          type: array
          items:
            type: object
            properties:
              question: { type: string }
              count:    { type: integer }
        confusionTopics:
          type: array
          items:
            type: object
            properties:
              topic:    { type: string }
              mentions: { type: integer }
        avgMessagesPerSession: { type: number, format: float }
```

---

## SignalR Hub Reference

**Hub:** `/hubs/gamification` | **Auth:** `?access_token={JWT}`

```
# Server → Client messages

BadgeEarned:
  badgeId:    uuid
  badgeName:  string
  iconUrl:    uri
  rarity:     common | rare | epic | legendary
  xpAwarded:  integer

LevelUp:
  newLevel:      integer
  totalXp:       integer
  xpToNextLevel: integer

XpAwarded:
  amount:    integer
  newTotal:  integer
  reason:    string

StreakUpdated:
  currentStreak: integer
  freezeTokens:  integer

# Client → Server (none — hub is push-only)
```

---

*Document: LMS Phase 2 OpenAPI Specifications · Version 1.0 · April 2026*
*Next: Phase 3 OpenAPI Specifications (Marketplace · Skills · Peer Review · GDPR · Audit · White-label · Compliance · PWA · i18n)*
