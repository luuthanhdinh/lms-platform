# LMS Platform — Phase 3 OpenAPI Specifications

**Phase:** 3 — Scale & Ecosystem
**OpenAPI version:** 3.1.0
**Services:** Marketplace · Skills & Competency · Peer Review · Cohort · AI Course Generator · GDPR · Audit Log · Tenant (White-label) · Compliance · Revenue

---

## Table of Contents

1. [Marketplace Service](#1-marketplace-service)
2. [Skills & Competency Service](#2-skills--competency-service)
3. [Peer Review Service](#3-peer-review-service)
4. [Cohort & Mentor Service](#4-cohort--mentor-service)
5. [AI Course Generator Service](#5-ai-course-generator-service)
6. [GDPR Service](#6-gdpr-service)
7. [Audit Log Service](#7-audit-log-service)
8. [Tenant Service — Phase 3 Extensions](#8-tenant-service--phase-3-extensions)
9. [Compliance Service](#9-compliance-service)
10. [Revenue Worker — Instructor Earnings API](#10-revenue-worker--instructor-earnings-api)

---

## 1. Marketplace Service

**Base URL:** `/api/marketplace` | **Port:** 5301 | **DB:** PostgreSQL + Elasticsearch

```yaml
openapi: 3.1.0
info:
  title: LMS Marketplace Service
  version: 1.0.0
  description: >
    Course marketplace with Elasticsearch-powered search and facets.
    Courses submitted by instructors and curated before public listing.
    Supports reviews, ratings, and bundle products.

servers:
  - url: /api/marketplace

tags:
  - name: Search
  - name: Listings
  - name: Reviews
  - name: Bundles
  - name: Curation

paths:

  /search:
    get:
      tags: [Search]
      summary: Full-text search with facets (public)
      operationId: searchCourses
      security: []
      parameters:
        - { name: q,          in: query, schema: { type: string }, description: Full-text query }
        - { name: category,   in: query, schema: { type: string } }
        - { name: tags,       in: query, schema: { type: array, items: { type: string } }, style: form, explode: true }
        - { name: difficulty, in: query, schema: { type: string, enum: [beginner, intermediate, advanced] } }
        - { name: language,   in: query, schema: { type: string } }
        - { name: priceMin,   in: query, schema: { type: integer, minimum: 0 } }
        - { name: priceMax,   in: query, schema: { type: integer, minimum: 0 } }
        - { name: ratingMin,  in: query, schema: { type: number, format: float, minimum: 0, maximum: 5 } }
        - { name: durationMax,in: query, schema: { type: integer, description: Max total seconds } }
        - { name: sort,       in: query, schema: { type: string, enum: [relevance, newest, rating, enrollment_count, price_asc, price_desc], default: relevance } }
        - { name: page,       in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize,   in: query, schema: { type: integer, default: 24, maximum: 48 } }
      responses:
        '200':
          description: Search results with facets
          content:
            application/json:
              schema: { $ref: '#/components/schemas/SearchResponse' }

  /listings:
    post:
      tags: [Listings]
      summary: Submit a course for marketplace listing (instructor)
      operationId: submitListing
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SubmitListingRequest' }
      responses:
        '201':
          description: Listing submitted for curation
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MarketplaceListing' }
        '409': { description: Already listed or pending review }

  /listings/me:
    get:
      tags: [Listings]
      summary: Get all listings submitted by current instructor
      operationId: getMyListings
      responses:
        '200':
          description: Listing list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MarketplaceListing' } }

  /listings/{listingId}:
    get:
      tags: [Listings]
      summary: Get listing detail (public)
      operationId: getListing
      security: []
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Listing
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MarketplaceListingDetail' }
        '404': { $ref: '#/components/responses/NotFound' }

    put:
      tags: [Listings]
      summary: Update listing metadata (instructor, only if draft/returned)
      operationId: updateListing
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateListingRequest' }
      responses:
        '200':
          description: Updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MarketplaceListing' }
        '409': { description: Cannot edit when status=approved or under_review }

    delete:
      tags: [Listings]
      summary: Remove listing from marketplace (instructor/admin)
      operationId: removeListing
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Listing removed }
        '409': { description: Active enrollments exist }

  /listings/{listingId}/approve:
    post:
      tags: [Curation]
      summary: Approve listing (curator role)
      operationId: approveListing
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Approved and indexed in Elasticsearch
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MarketplaceListing' }

  /listings/{listingId}/reject:
    post:
      tags: [Curation]
      summary: Reject listing (curator role)
      operationId: rejectListing
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
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
        '200':
          description: Rejected (instructor notified)
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MarketplaceListing' }

  /listings/{listingId}/reviews:
    get:
      tags: [Reviews]
      summary: Get reviews for listing (public)
      operationId: getReviews
      security: []
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: sort, in: query, schema: { type: string, enum: [newest, highest, lowest], default: newest } }
        - { name: page, in: query, schema: { type: integer, default: 1 } }
      responses:
        '200':
          description: Reviews
          content:
            application/json:
              schema:
                type: object
                properties:
                  avgRating: { type: number, format: float }
                  totalCount: { type: integer }
                  data: { type: array, items: { $ref: '#/components/schemas/Review' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

    post:
      tags: [Reviews]
      summary: Submit review (enrolled and >= 25% complete)
      operationId: submitReview
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateReviewRequest' }
      responses:
        '201':
          description: Review submitted
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Review' }
        '403': { description: Not enrolled or completion < 25% }
        '409': { description: Already reviewed }

  /listings/{listingId}/reviews/{reviewId}:
    delete:
      tags: [Reviews]
      summary: Remove review (author or moderator)
      operationId: deleteReview
      parameters:
        - { name: listingId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: reviewId,  in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Deleted }

  /bundles:
    post:
      tags: [Bundles]
      summary: Create a course bundle (instructor/admin)
      operationId: createBundle
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateBundleRequest' }
      responses:
        '201':
          description: Bundle created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Bundle' }

  /bundles/{bundleId}:
    get:
      tags: [Bundles]
      summary: Get bundle detail (public)
      operationId: getBundle
      security: []
      parameters:
        - { name: bundleId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Bundle
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Bundle' }

  /curation/queue:
    get:
      tags: [Curation]
      summary: Get listings pending curation (curator role)
      operationId: getCurationQueue
      parameters:
        - { name: page, in: query, schema: { type: integer, default: 1 } }
      responses:
        '200':
          description: Queue
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MarketplaceListing' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

components:
  schemas:
    SubmitListingRequest:
      type: object
      required: [courseId, price, currency, categories]
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid' }
        price:       { type: integer, minimum: 0, description: Cents; 0 = free }
        currency:    { type: string, default: usd }
        categories:  { type: array, minItems: 1, maxItems: 3, items: { type: string } }
        targetAudience: { type: string, maxLength: 500 }
        prerequisites:  { type: string, maxLength: 500 }

    UpdateListingRequest:
      type: object
      properties:
        price:       { type: integer, minimum: 0 }
        categories:  { type: array, items: { type: string } }
        targetAudience: { type: string }
        prerequisites:  { type: string }

    MarketplaceListing:
      type: object
      properties:
        id:           { $ref: '#/components/schemas/Uuid' }
        courseId:     { $ref: '#/components/schemas/Uuid' }
        instructorId: { $ref: '#/components/schemas/Uuid' }
        tenantId:     { $ref: '#/components/schemas/Uuid' }
        status:       { type: string, enum: [draft, under_review, approved, rejected, removed] }
        price:        { type: integer }
        currency:     { type: string }
        categories:   { type: array, items: { type: string } }
        avgRating:    { type: number, format: float }
        reviewCount:  { type: integer }
        submittedAt:  { type: string, format: date-time }
        reviewedAt:   { type: string, format: date-time, nullable: true }

    MarketplaceListingDetail:
      allOf:
        - { $ref: '#/components/schemas/MarketplaceListing' }
        - type: object
          properties:
            courseTitle:        { type: string }
            courseDescription:  { type: string }
            thumbnailUrl:       { type: string, format: uri, nullable: true }
            totalDurationSeconds: { type: integer }
            lessonCount:        { type: integer }
            instructorName:     { type: string }
            instructorBio:      { type: string, nullable: true }
            targetAudience:     { type: string }
            prerequisites:      { type: string }
            languages:
              type: array
              items: { type: string }

    SearchResponse:
      type: object
      properties:
        hits: { type: integer }
        facets:
          type: object
          properties:
            categories:
              type: array
              items:
                type: object
                properties:
                  value: { type: string }
                  count: { type: integer }
            languages:
              type: array
              items:
                type: object
                properties:
                  value: { type: string }
                  count: { type: integer }
        data:
          type: array
          items: { $ref: '#/components/schemas/MarketplaceListingDetail' }
        meta: { $ref: '#/components/schemas/PaginatedMeta' }

    Review:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        listingId:   { $ref: '#/components/schemas/Uuid' }
        userId:      { $ref: '#/components/schemas/Uuid' }
        displayName: { type: string }
        rating:      { type: integer, minimum: 1, maximum: 5 }
        title:       { type: string, nullable: true }
        body:        { type: string, nullable: true }
        createdAt:   { type: string, format: date-time }

    CreateReviewRequest:
      type: object
      required: [rating]
      properties:
        rating: { type: integer, minimum: 1, maximum: 5 }
        title:  { type: string, maxLength: 200, nullable: true }
        body:   { type: string, maxLength: 2000, nullable: true }

    CreateBundleRequest:
      type: object
      required: [name, courseIds, price]
      properties:
        name:        { type: string, maxLength: 200 }
        description: { type: string, maxLength: 1000 }
        courseIds:   { type: array, minItems: 2, maxItems: 20, items: { $ref: '#/components/schemas/Uuid' } }
        price:       { type: integer, minimum: 0 }
        currency:    { type: string, default: usd }

    Bundle:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        name:        { type: string }
        description: { type: string }
        price:       { type: integer }
        currency:    { type: string }
        savings:     { type: integer, description: Cents saved vs individual prices }
        courses:
          type: array
          items:
            type: object
            properties:
              courseId: { $ref: '#/components/schemas/Uuid' }
              title:    { type: string }
              price:    { type: integer }
```

---

## 2. Skills & Competency Service

**Base URL:** `/api/skills` | **Port:** 5302 | **DB:** `lms_skills` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Skills & Competency Service
  version: 1.0.0
  description: >
    Manages skill taxonomy (SFIA 9 seed + custom), skill maps,
    mastery levels, and skill gap dashboards for org admins.

servers:
  - url: /api/skills

tags:
  - name: Taxonomy
  - name: UserSkills
  - name: OrgAdmin

paths:

  /taxonomy:
    get:
      tags: [Taxonomy]
      summary: Get full skill taxonomy (public within tenant)
      operationId: getTaxonomy
      parameters:
        - { name: domain, in: query, schema: { type: string } }
        - { name: level,  in: query, schema: { type: integer, minimum: 1, maximum: 7 } }
      responses:
        '200':
          description: Skill list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/Skill' } }

  /taxonomy:
    post:
      tags: [Taxonomy]
      summary: Add custom skill to taxonomy (admin)
      operationId: createSkill
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateSkillRequest' }
      responses:
        '201':
          description: Skill created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Skill' }

  /taxonomy/{skillId}:
    get:
      tags: [Taxonomy]
      summary: Get skill detail with mapped courses
      operationId: getSkill
      parameters:
        - { name: skillId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Skill detail
          content:
            application/json:
              schema: { $ref: '#/components/schemas/SkillDetail' }

  /taxonomy/{skillId}/map-course:
    post:
      tags: [Taxonomy]
      summary: Map a course to a skill (instructor/admin)
      operationId: mapCourse
      parameters:
        - { name: skillId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [courseId, masteryGain]
              properties:
                courseId:    { $ref: '#/components/schemas/Uuid' }
                masteryGain: { type: number, format: float, minimum: 0.01, maximum: 1.0 }
      responses:
        '201': { description: Mapped }

  /me:
    get:
      tags: [UserSkills]
      summary: Get current user skill profile
      operationId: getMySkills
      responses:
        '200':
          description: Skill profile
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UserSkillProfile' }

  /me/goals:
    post:
      tags: [UserSkills]
      summary: Set target skill goals for self-directed learning
      operationId: setSkillGoals
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [goals]
              properties:
                goals:
                  type: array
                  maxItems: 10
                  items:
                    type: object
                    required: [skillId, targetLevel]
                    properties:
                      skillId:     { $ref: '#/components/schemas/Uuid' }
                      targetLevel: { type: integer, minimum: 1, maximum: 7 }
      responses:
        '200': { description: Goals updated }

  /org/gap-analysis:
    get:
      tags: [OrgAdmin]
      summary: Org-wide skill gap analysis (org-admin)
      operationId: getOrgGapAnalysis
      parameters:
        - { name: department, in: query, schema: { type: string } }
        - { name: domain,     in: query, schema: { type: string } }
      responses:
        '200':
          description: Gap analysis
          content:
            application/json:
              schema: { $ref: '#/components/schemas/OrgGapAnalysis' }

  /org/required:
    post:
      tags: [OrgAdmin]
      summary: Set required skills for a role/department (org-admin)
      operationId: setRequiredSkills
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/RequiredSkillsRequest' }
      responses:
        '200': { description: Requirements saved }

components:
  schemas:
    Skill:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        name:        { type: string }
        slug:        { type: string }
        domain:      { type: string }
        category:    { type: string }
        sfiaLevel:   { type: integer, minimum: 1, maximum: 7, nullable: true }
        description: { type: string }
        isBuiltIn:   { type: boolean }
        courseCount: { type: integer }

    SkillDetail:
      allOf:
        - { $ref: '#/components/schemas/Skill' }
        - type: object
          properties:
            mappedCourses:
              type: array
              items:
                type: object
                properties:
                  courseId:    { $ref: '#/components/schemas/Uuid' }
                  courseTitle: { type: string }
                  masteryGain: { type: number, format: float }

    CreateSkillRequest:
      type: object
      required: [name, domain]
      properties:
        name:     { type: string, maxLength: 200 }
        domain:   { type: string }
        category: { type: string }
        description: { type: string, maxLength: 1000 }

    UserSkillProfile:
      type: object
      properties:
        userId: { $ref: '#/components/schemas/Uuid' }
        skills:
          type: array
          items:
            type: object
            properties:
              skill:        { $ref: '#/components/schemas/Skill' }
              masteryScore: { type: number, format: float }
              currentLevel: { type: integer }
              targetLevel:  { type: integer, nullable: true }
              gap:          { type: number, format: float }
        totalSkills:    { type: integer }
        completedGoals: { type: integer }
        pendingGoals:   { type: integer }

    OrgGapAnalysis:
      type: object
      properties:
        generatedAt: { type: string, format: date-time }
        criticalGaps:
          type: array
          items:
            type: object
            properties:
              skill:            { $ref: '#/components/schemas/Skill' }
              avgMastery:       { type: number, format: float }
              requiredLevel:    { type: integer }
              usersGapped:      { type: integer }
              recommendedCourses:
                type: array
                items: { $ref: '#/components/schemas/Uuid' }
        departments:
          type: array
          items:
            type: object
            properties:
              department: { type: string }
              avgMastery: { type: number, format: float }
              topGaps:    { type: array, items: { type: string } }

    RequiredSkillsRequest:
      type: object
      required: [skills]
      properties:
        role:        { type: string, nullable: true }
        department:  { type: string, nullable: true }
        skills:
          type: array
          items:
            type: object
            required: [skillId, requiredLevel]
            properties:
              skillId:       { $ref: '#/components/schemas/Uuid' }
              requiredLevel: { type: integer, minimum: 1, maximum: 7 }
```

---

## 3. Peer Review Service

**Base URL:** `/api/peer-review` | **Port:** 5303 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS Peer Review Service
  version: 1.0.0
  description: >
    Double-blind peer review workflow for project assignments.
    Each submission gets N reviews (configurable); grades aggregated
    with outlier detection (drop scores > 2σ from mean).

servers:
  - url: /api/peer-review

tags:
  - name: Assignments
  - name: Submissions
  - name: Reviewing

paths:

  /assignments:
    post:
      tags: [Assignments]
      summary: Create peer review assignment (instructor)
      operationId: createAssignment
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreatePeerAssignmentRequest' }
      responses:
        '201':
          description: Assignment created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PeerAssignment' }

  /assignments/{assignmentId}:
    get:
      tags: [Assignments]
      summary: Get assignment detail
      operationId: getPeerAssignment
      parameters:
        - { $ref: '#/components/parameters/assignmentId' }
      responses:
        '200':
          description: Assignment
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PeerAssignment' }

  /assignments/{assignmentId}/submit:
    post:
      tags: [Submissions]
      summary: Submit project work
      operationId: submitWork
      parameters:
        - { $ref: '#/components/parameters/assignmentId' }
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required: [content]
              properties:
                content: { type: string, description: Text content or URL }
                attachments:
                  type: array
                  maxItems: 5
                  items: { type: string, format: binary }
      responses:
        '201':
          description: Submission received
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PeerSubmission' }
        '409': { description: Already submitted or deadline passed }

  /assignments/{assignmentId}/reviews/me:
    get:
      tags: [Reviewing]
      summary: Get reviews assigned to current user
      operationId: getMyReviews
      parameters:
        - { $ref: '#/components/parameters/assignmentId' }
      responses:
        '200':
          description: Submissions to review (anonymised)
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/AnonymousSubmission' }

  /reviews/{reviewId}/submit:
    post:
      tags: [Reviewing]
      summary: Submit a peer review
      operationId: submitReview
      parameters:
        - { name: reviewId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SubmitReviewRequest' }
      responses:
        '200':
          description: Review submitted
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PeerReview' }
        '409': { description: Already submitted }

  /submissions/{submissionId}/result:
    get:
      tags: [Submissions]
      summary: Get aggregated result (available after all N reviews submitted)
      operationId: getSubmissionResult
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Aggregated result
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AggregatedResult' }
        '404': { $ref: '#/components/responses/NotFound' }

  /assignments/{assignmentId}/analytics:
    get:
      tags: [Assignments]
      summary: Review completion analytics (instructor)
      operationId: getPeerAssignmentAnalytics
      parameters:
        - { $ref: '#/components/parameters/assignmentId' }
      responses:
        '200':
          description: Analytics
          content:
            application/json:
              schema:
                type: object
                properties:
                  totalSubmissions: { type: integer }
                  pendingReviews:   { type: integer }
                  completedReviews: { type: integer }
                  avgScore:         { type: number, format: float }
                  outlierCount:     { type: integer }

components:
  parameters:
    assignmentId:
      name: assignmentId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CreatePeerAssignmentRequest:
      type: object
      required: [courseId, title, rubric, reviewCount, submissionDeadline, reviewDeadline]
      properties:
        courseId:          { $ref: '#/components/schemas/Uuid' }
        title:             { type: string, maxLength: 200 }
        description:       { type: string }
        rubric:
          type: array
          minItems: 1
          maxItems: 8
          items:
            type: object
            required: [name, maxPoints]
            properties:
              name:      { type: string }
              maxPoints: { type: integer, minimum: 1 }
              guidance:  { type: string, nullable: true }
        reviewCount:       { type: integer, minimum: 2, maximum: 10 }
        submissionDeadline:{ type: string, format: date-time }
        reviewDeadline:    { type: string, format: date-time }

    PeerAssignment:
      type: object
      properties:
        id:                { $ref: '#/components/schemas/Uuid' }
        courseId:          { $ref: '#/components/schemas/Uuid' }
        tenantId:          { $ref: '#/components/schemas/Uuid' }
        title:             { type: string }
        rubric:            { type: array, items: { type: object } }
        reviewCount:       { type: integer }
        submissionDeadline:{ type: string, format: date-time }
        reviewDeadline:    { type: string, format: date-time }
        mySubmissionStatus:{ type: string, enum: [not_submitted, submitted, reviewing, completed], nullable: true }

    PeerSubmission:
      type: object
      properties:
        id:         { $ref: '#/components/schemas/Uuid' }
        assignmentId: { $ref: '#/components/schemas/Uuid' }
        submittedAt:{ type: string, format: date-time }
        content:    { type: string }
        status:     { type: string, enum: [submitted, under_review, graded] }

    AnonymousSubmission:
      type: object
      properties:
        reviewId:   { $ref: '#/components/schemas/Uuid' }
        content:    { type: string }
        attachments:{ type: array, items: { type: string, format: uri } }
        rubric:     { type: array, items: { type: object } }

    SubmitReviewRequest:
      type: object
      required: [criteriaScores]
      properties:
        criteriaScores:
          type: array
          items:
            type: object
            required: [criterionIndex, score]
            properties:
              criterionIndex: { type: integer, minimum: 0 }
              score:          { type: integer, minimum: 0 }
              comment:        { type: string, nullable: true }
        overallFeedback: { type: string, maxLength: 2000 }

    PeerReview:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        submissionId:    { $ref: '#/components/schemas/Uuid' }
        totalScore:      { type: number, format: float }
        criteriaScores:  { type: array, items: { type: object } }
        overallFeedback: { type: string, nullable: true }
        submittedAt:     { type: string, format: date-time }

    AggregatedResult:
      type: object
      properties:
        submissionId:    { $ref: '#/components/schemas/Uuid' }
        aggregateScore:  { type: number, format: float }
        maxScore:        { type: number, format: float }
        percentage:      { type: number, format: float }
        reviewCount:     { type: integer }
        outliersDropped: { type: integer }
        reviews:
          type: array
          items: { $ref: '#/components/schemas/PeerReview' }
```

---

## 4. Cohort & Mentor Service

**Base URL:** `/api/cohorts` | **Port:** 5304 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS Cohort & Mentor Service
  version: 1.0.0
  description: >
    Manages study groups, learner cohorts, and mentor-mentee pairings.
    Mentor availability set via a calendar slot model.

servers:
  - url: /api/cohorts

tags:
  - name: StudyGroups
  - name: Cohorts
  - name: Mentoring

paths:

  /study-groups:
    post:
      tags: [StudyGroups]
      summary: Create a study group (any enrolled student)
      operationId: createStudyGroup
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateStudyGroupRequest' }
      responses:
        '201':
          description: Group created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StudyGroup' }

  /study-groups/courses/{courseId}:
    get:
      tags: [StudyGroups]
      summary: List study groups for a course
      operationId: listStudyGroups
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Study groups
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/StudyGroup' } }

  /study-groups/{groupId}/join:
    post:
      tags: [StudyGroups]
      summary: Join a study group
      operationId: joinStudyGroup
      parameters:
        - { name: groupId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200': { description: Joined }
        '409': { description: Already member or group full }

  /study-groups/{groupId}/leave:
    post:
      tags: [StudyGroups]
      summary: Leave a study group
      operationId: leaveStudyGroup
      parameters:
        - { name: groupId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Left }

  /cohorts:
    post:
      tags: [Cohorts]
      summary: Create a cohort (instructor/admin)
      operationId: createCohort
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateCohortRequest' }
      responses:
        '201':
          description: Cohort created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Cohort' }

  /cohorts/{cohortId}/enroll:
    post:
      tags: [Cohorts]
      summary: Bulk enroll students into cohort (instructor/admin)
      operationId: cohortEnroll
      parameters:
        - { name: cohortId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [userIds]
              properties:
                userIds: { type: array, maxItems: 1000, items: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Enrollment result
          content:
            application/json:
              schema:
                type: object
                properties:
                  enrolled: { type: integer }
                  failed:   { type: integer }

  /mentors:
    get:
      tags: [Mentoring]
      summary: Browse available mentors
      operationId: listMentors
      parameters:
        - { name: courseId, in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: available, in: query, schema: { type: boolean } }
      responses:
        '200':
          description: Mentor list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MentorProfile' } }

  /mentors/apply:
    post:
      tags: [Mentoring]
      summary: Apply to become a mentor (student who completed course)
      operationId: applyMentor
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [courseId, bio]
              properties:
                courseId:       { $ref: '#/components/schemas/Uuid' }
                bio:            { type: string, maxLength: 1000 }
                availableSlots: { type: integer, minimum: 1, maximum: 10 }
      responses:
        '201': { description: Application submitted }

  /mentors/{mentorId}/request:
    post:
      tags: [Mentoring]
      summary: Request a mentor session
      operationId: requestMentor
      parameters:
        - { name: mentorId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [courseId, message]
              properties:
                courseId: { $ref: '#/components/schemas/Uuid' }
                message:  { type: string, maxLength: 500 }
      responses:
        '201': { description: Request sent to mentor }
        '409': { description: Mentor at capacity }

  /mentoring/active:
    get:
      tags: [Mentoring]
      summary: Get active mentor-mentee pairs for current user
      operationId: getActivePairs
      responses:
        '200':
          description: Active pairs
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MentorPair' } }

  /mentoring/{pairId}/close:
    post:
      tags: [Mentoring]
      summary: Close a mentoring relationship with rating
      operationId: closePair
      parameters:
        - { name: pairId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [rating]
              properties:
                rating:  { type: integer, minimum: 1, maximum: 5 }
                comment: { type: string, maxLength: 500 }
      responses:
        '200': { description: Pair closed and MentorMatchClosed event published }

components:
  schemas:
    CreateStudyGroupRequest:
      type: object
      required: [courseId, name]
      properties:
        courseId:  { $ref: '#/components/schemas/Uuid' }
        name:      { type: string, maxLength: 100 }
        maxSize:   { type: integer, minimum: 2, maximum: 50, default: 10 }
        isPrivate: { type: boolean, default: false }

    StudyGroup:
      type: object
      properties:
        id:        { $ref: '#/components/schemas/Uuid' }
        courseId:  { $ref: '#/components/schemas/Uuid' }
        name:      { type: string }
        createdBy: { $ref: '#/components/schemas/Uuid' }
        maxSize:   { type: integer }
        memberCount:{ type: integer }
        isPrivate: { type: boolean }
        createdAt: { type: string, format: date-time }

    CreateCohortRequest:
      type: object
      required: [courseId, name, startDate, endDate]
      properties:
        courseId:  { $ref: '#/components/schemas/Uuid' }
        name:      { type: string, maxLength: 200 }
        startDate: { type: string, format: date }
        endDate:   { type: string, format: date }

    Cohort:
      type: object
      properties:
        id:        { $ref: '#/components/schemas/Uuid' }
        courseId:  { $ref: '#/components/schemas/Uuid' }
        name:      { type: string }
        startDate: { type: string, format: date }
        endDate:   { type: string, format: date }
        memberCount:{ type: integer }
        status:    { type: string, enum: [upcoming, active, completed] }

    MentorProfile:
      type: object
      properties:
        userId:         { $ref: '#/components/schemas/Uuid' }
        displayName:    { type: string }
        avatarUrl:      { type: string, format: uri, nullable: true }
        bio:            { type: string }
        rating:         { type: number, format: float }
        reviewCount:    { type: integer }
        availableSlots: { type: integer }
        courseIds:      { type: array, items: { $ref: '#/components/schemas/Uuid' } }

    MentorPair:
      type: object
      properties:
        id:         { $ref: '#/components/schemas/Uuid' }
        mentorId:   { $ref: '#/components/schemas/Uuid' }
        menteeId:   { $ref: '#/components/schemas/Uuid' }
        courseId:   { $ref: '#/components/schemas/Uuid' }
        status:     { type: string, enum: [pending, active, closed] }
        startedAt:  { type: string, format: date-time, nullable: true }
        closedAt:   { type: string, format: date-time, nullable: true }
```

---

## 5. AI Course Generator Service

**Base URL:** `/api/course-gen` | **Port:** 5305 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS AI Course Generator Service
  version: 1.0.0
  description: >
    Generates course outlines, section titles, lesson descriptions,
    and placeholder quiz questions from a prompt. Instructor reviews
    and approves before publishing. Does NOT generate media content.

servers:
  - url: /api/course-gen

tags:
  - name: Jobs
  - name: Review

paths:

  /generate:
    post:
      tags: [Jobs]
      summary: Start course generation job (instructor)
      operationId: generateCourse
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/GenerationRequest' }
      responses:
        '202':
          description: Job started
          content:
            application/json:
              schema:
                type: object
                properties:
                  jobId:  { $ref: '#/components/schemas/Uuid' }
                  status: { type: string, enum: [queued] }

  /jobs/{jobId}:
    get:
      tags: [Jobs]
      summary: Get generation job status and result
      operationId: getJobStatus
      parameters:
        - { name: jobId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Job status
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GenerationJob' }

  /jobs/{jobId}/approve:
    post:
      tags: [Review]
      summary: Approve generated outline and create course draft
      operationId: approveGeneration
      parameters:
        - { name: jobId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              properties:
                editedOutline: { $ref: '#/components/schemas/GeneratedOutline', description: Optional instructor edits }
      responses:
        '201':
          description: Course draft created in CourseService
          content:
            application/json:
              schema:
                type: object
                properties:
                  courseId: { $ref: '#/components/schemas/Uuid' }

  /jobs/{jobId}/discard:
    delete:
      tags: [Review]
      summary: Discard generated output without creating course
      operationId: discardGeneration
      parameters:
        - { name: jobId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Discarded }

components:
  schemas:
    GenerationRequest:
      type: object
      required: [prompt, targetAudience, difficulty, language]
      properties:
        prompt:
          type: string
          maxLength: 2000
          description: Description of the course topic and goals
        targetAudience: { type: string, maxLength: 500 }
        difficulty:     { type: string, enum: [beginner, intermediate, advanced] }
        language:       { type: string }
        sectionCount:   { type: integer, minimum: 2, maximum: 20, default: 5 }
        lessonsPerSection: { type: integer, minimum: 1, maximum: 10, default: 4 }
        includeQuizzes: { type: boolean, default: true }

    GenerationJob:
      type: object
      properties:
        id:        { $ref: '#/components/schemas/Uuid' }
        status:    { type: string, enum: [queued, generating, completed, failed] }
        result:    { $ref: '#/components/schemas/GeneratedOutline', nullable: true }
        error:     { type: string, nullable: true }
        createdAt: { type: string, format: date-time }
        completedAt: { type: string, format: date-time, nullable: true }
        tokenCost: { type: integer, nullable: true }

    GeneratedOutline:
      type: object
      properties:
        title:       { type: string }
        description: { type: string }
        sections:
          type: array
          items:
            type: object
            properties:
              title:   { type: string }
              lessons:
                type: array
                items:
                  type: object
                  properties:
                    title:       { type: string }
                    description: { type: string }
                    contentType: { type: string, enum: [video, pdf] }
                    quizzes:
                      type: array
                      nullable: true
                      items:
                        type: object
                        properties:
                          prompt:  { type: string }
                          options: { type: array, items: { type: string } }
                          correctIndex: { type: integer }
```

---

## 6. GDPR Service

**Base URL:** `/api/gdpr` | **Port:** 5306 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS GDPR Service
  version: 1.0.0
  description: >
    Handles GDPR data subject rights via Saga pattern.
    All erasure and export requests are async; status polled by ID.
    Each participating service subscribes to GdprErasureRequested
    and responds with UserDataErased event.

servers:
  - url: /api/gdpr

tags:
  - name: Erasure
  - name: Export
  - name: ConsentLog

paths:

  /erasure:
    post:
      tags: [Erasure]
      summary: Request data erasure (student or admin)
      operationId: requestErasure
      description: >
        Publishes GdprErasureRequested event. Each service
        anonymises its own data. Saga tracks completion.
        All active enrollments and sessions terminated.
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [reason]
              properties:
                reason: { type: string, enum: [user_request, legal, account_closure] }
      responses:
        '202':
          description: Request accepted
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GdprRequest' }

  /erasure/{requestId}/admin:
    post:
      tags: [Erasure]
      summary: Request erasure for any user (admin only)
      operationId: requestErasureForUser
      parameters:
        - { name: requestId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [userId, reason]
              properties:
                userId: { $ref: '#/components/schemas/Uuid' }
                reason: { type: string }
      responses:
        '202':
          description: Erasure initiated for user
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GdprRequest' }

  /requests/{requestId}:
    get:
      tags: [Erasure, Export]
      summary: Get GDPR request status
      operationId: getGdprRequestStatus
      parameters:
        - { name: requestId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Request status
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GdprRequestStatus' }

  /export:
    post:
      tags: [Export]
      summary: Request personal data export
      operationId: requestDataExport
      description: Returns a download URL when complete (up to 24h processing time).
      responses:
        '202':
          description: Export queued
          content:
            application/json:
              schema: { $ref: '#/components/schemas/GdprRequest' }

  /export/{requestId}/download:
    get:
      tags: [Export]
      summary: Download personal data export (when status = completed)
      operationId: downloadExport
      parameters:
        - { name: requestId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Signed download URL (TTL 1 hour)
          content:
            application/json:
              schema:
                type: object
                properties:
                  downloadUrl: { type: string, format: uri }
                  expiresAt:   { type: string, format: date-time }
        '409': { description: Export not yet ready }

  /consent-log:
    get:
      tags: [ConsentLog]
      summary: Get full consent history for current user
      operationId: getConsentLog
      responses:
        '200':
          description: Consent log
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/ConsentEvent' }

components:
  schemas:
    GdprRequest:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        userId:      { $ref: '#/components/schemas/Uuid' }
        type:        { type: string, enum: [erasure, export] }
        status:      { type: string, enum: [pending, processing, completed, failed] }
        requestedAt: { type: string, format: date-time }

    GdprRequestStatus:
      allOf:
        - { $ref: '#/components/schemas/GdprRequest' }
        - type: object
          properties:
            services:
              type: array
              items:
                type: object
                properties:
                  serviceName: { type: string }
                  status:      { type: string, enum: [pending, completed, failed] }
                  completedAt: { type: string, format: date-time, nullable: true }
            completedAt: { type: string, format: date-time, nullable: true }

    ConsentEvent:
      type: object
      properties:
        consentType: { type: string }
        granted:     { type: boolean }
        occurredAt:  { type: string, format: date-time }
        ipAddress:   { type: string, nullable: true }
        userAgent:   { type: string, nullable: true }
```

---

## 7. Audit Log Service

**Base URL:** `/api/audit` | **Port:** 5307 | **DB:** EventStoreDB

```yaml
openapi: 3.1.0
info:
  title: LMS Audit Log Service
  version: 1.0.0
  description: >
    Immutable append-only audit log backed by EventStoreDB.
    Written by all services via AuditEvent messages on the bus.
    Queryable by admin for compliance reporting.

servers:
  - url: /api/audit

tags:
  - name: AuditLog
  - name: Export

paths:

  /:
    get:
      tags: [AuditLog]
      summary: Query audit log (admin only)
      operationId: queryAuditLog
      parameters:
        - { name: actorId,       in: query, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: resourceType,  in: query, schema: { type: string } }
        - { name: resourceId,    in: query, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: action,        in: query, schema: { type: string } }
        - { name: from,          in: query, schema: { type: string, format: date-time } }
        - { name: to,            in: query, schema: { type: string, format: date-time } }
        - { name: page,          in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize,      in: query, schema: { type: integer, default: 50, maximum: 500 } }
      responses:
        '200':
          description: Audit events
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/AuditEvent' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /export:
    post:
      tags: [Export]
      summary: Export audit log to CSV (admin only)
      operationId: exportAuditLog
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              properties:
                from:         { type: string, format: date-time }
                to:           { type: string, format: date-time }
                resourceType: { type: string }
                format:       { type: string, enum: [csv, json], default: csv }
      responses:
        '202':
          description: Export queued
          content:
            application/json:
              schema:
                type: object
                properties:
                  exportId:    { $ref: '#/components/schemas/Uuid' }
                  downloadUrl: { type: string, format: uri, nullable: true }

components:
  schemas:
    AuditEvent:
      type: object
      properties:
        id:           { $ref: '#/components/schemas/Uuid' }
        tenantId:     { $ref: '#/components/schemas/Uuid' }
        actorId:      { $ref: '#/components/schemas/Uuid' }
        actorEmail:   { type: string }
        actorRole:    { type: string }
        action:       { type: string }
        resourceType: { type: string }
        resourceId:   { $ref: '#/components/schemas/Uuid' }
        ipAddress:    { type: string, nullable: true }
        userAgent:    { type: string, nullable: true }
        metadata:     { type: object }
        occurredAt:   { type: string, format: date-time }
```

---

## 8. Tenant Service — Phase 3 Extensions

Extends Phase 1 IdentityService tenant endpoints. Base URL: `/api/tenant`

```yaml
openapi: 3.1.0
info:
  title: LMS Tenant Service (Phase 3 extensions)
  version: 1.0.0
  description: >
    White-label, custom domain, SSO federation, and branding configuration.
    Managed by platform admin (not org-admin).

servers:
  - url: /api/tenant

tags:
  - name: WhiteLabel
  - name: Domains
  - name: Branding
  - name: Blockchain

paths:

  /{tenantId}/domain:
    put:
      tags: [Domains]
      summary: Set custom domain for tenant (platform admin)
      operationId: setCustomDomain
      parameters:
        - { name: tenantId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [domain]
              properties:
                domain: { type: string, format: hostname, example: learn.mycompany.com }
      responses:
        '200':
          description: Domain set (cert provisioning initiated via cert-manager)
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TenantDomain' }
        '409': { description: Domain already in use }

  /{tenantId}/domain/verify:
    post:
      tags: [Domains]
      summary: Verify domain DNS records are configured
      operationId: verifyDomain
      parameters:
        - { name: tenantId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: DNS verification result
          content:
            application/json:
              schema:
                type: object
                properties:
                  verified:  { type: boolean }
                  certReady: { type: boolean }
                  errors:
                    type: array
                    items: { type: string }

  /{tenantId}/branding:
    put:
      tags: [Branding]
      summary: Update white-label branding (org-admin)
      operationId: updateBranding
      parameters:
        - { name: tenantId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/TenantBranding' }
      responses:
        '200':
          description: Branding updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TenantBranding' }

  /{tenantId}/sso:
    put:
      tags: [WhiteLabel]
      summary: Configure SSO federation (SAML2/OIDC) (platform admin)
      operationId: configureSso
      parameters:
        - { name: tenantId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SsoConfig' }
      responses:
        '200':
          description: SSO configured in Keycloak identity provider
          content:
            application/json:
              schema: { $ref: '#/components/schemas/SsoConfig' }

  /{tenantId}/blockchain:
    put:
      tags: [Blockchain]
      summary: Enable/disable blockchain anchoring (platform admin)
      operationId: setBlockchainConfig
      parameters:
        - { name: tenantId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [enabled]
              properties:
                enabled: { type: boolean }
                network: { type: string, enum: [polygon-mainnet, polygon-amoy], nullable: true }
      responses:
        '200': { description: Config updated }

components:
  schemas:
    TenantDomain:
      type: object
      properties:
        domain:    { type: string }
        status:    { type: string, enum: [pending_dns, verified, cert_pending, active, failed] }
        verifiedAt:{ type: string, format: date-time, nullable: true }

    TenantBranding:
      type: object
      properties:
        primaryColor:   { type: string, pattern: '^#[0-9A-Fa-f]{6}$' }
        logoUrl:        { type: string, format: uri }
        faviconUrl:     { type: string, format: uri }
        emailFromName:  { type: string }
        emailFromAddr:  { type: string, format: email }
        metaTitle:      { type: string }
        footerText:     { type: string }
        hidePoweredBy:  { type: boolean }

    SsoConfig:
      type: object
      required: [protocol]
      properties:
        protocol:      { type: string, enum: [saml2, oidc] }
        metadataUrl:   { type: string, format: uri, nullable: true }
        clientId:      { type: string, nullable: true }
        discoveryUrl:  { type: string, format: uri, nullable: true }
        emailAttribute:{ type: string, default: email }
        roleMapping:   { type: object, nullable: true }
        autoProvisioning: { type: boolean, default: true }
```

---

## 9. Compliance Service

**Base URL:** `/api/compliance` | **Port:** 5308 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS Compliance Service
  version: 1.0.0
  description: >
    Manages mandatory training assignments, completion tracking,
    and automated escalation for overdue learners.

servers:
  - url: /api/compliance

tags:
  - name: Assignments
  - name: Reports

paths:

  /assignments:
    post:
      tags: [Assignments]
      summary: Create mandatory training assignment (org-admin)
      operationId: createMandatoryAssignment
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/MandatoryAssignmentRequest' }
      responses:
        '201':
          description: Assignment created and notifications sent
          content:
            application/json:
              schema: { $ref: '#/components/schemas/MandatoryAssignment' }

  /assignments:
    get:
      tags: [Assignments]
      summary: List mandatory assignments for tenant (org-admin)
      operationId: listMandatoryAssignments
      responses:
        '200':
          description: Assignments
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MandatoryAssignment' } }

  /assignments/me:
    get:
      tags: [Assignments]
      summary: Get mandatory training assignments for current user
      operationId: getMyAssignments
      responses:
        '200':
          description: My assignments with status
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/MyAssignment' } }

  /reports/{assignmentId}:
    get:
      tags: [Reports]
      summary: Completion report for mandatory assignment (org-admin)
      operationId: getComplianceReport
      parameters:
        - { name: assignmentId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Compliance report
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ComplianceReport' }

  /reports/{assignmentId}/export:
    get:
      tags: [Reports]
      summary: Export compliance report as CSV
      operationId: exportComplianceReport
      parameters:
        - { name: assignmentId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: CSV file
          content:
            text/csv:
              schema: { type: string }

components:
  schemas:
    MandatoryAssignmentRequest:
      type: object
      required: [courseId, title, deadline]
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid' }
        title:       { type: string, maxLength: 200 }
        deadline:    { type: string, format: date-time }
        userIds:     { type: array, items: { $ref: '#/components/schemas/Uuid' }, nullable: true }
        department:  { type: string, nullable: true, description: Assign to all in department }
        reminderDays:{ type: array, items: { type: integer }, default: [14, 7, 1] }

    MandatoryAssignment:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        courseId:        { $ref: '#/components/schemas/Uuid' }
        courseTitle:     { type: string }
        title:           { type: string }
        deadline:        { type: string, format: date-time }
        assignedCount:   { type: integer }
        completedCount:  { type: integer }
        overdueCount:    { type: integer }
        createdAt:       { type: string, format: date-time }

    MyAssignment:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        courseId:        { $ref: '#/components/schemas/Uuid' }
        courseTitle:     { type: string }
        title:           { type: string }
        deadline:        { type: string, format: date-time }
        status:          { type: string, enum: [not_started, in_progress, completed, overdue] }
        completionPct:   { type: number, format: float }
        completedAt:     { type: string, format: date-time, nullable: true }
        daysRemaining:   { type: integer }

    ComplianceReport:
      type: object
      properties:
        assignmentId:    { $ref: '#/components/schemas/Uuid' }
        title:           { type: string }
        deadline:        { type: string, format: date-time }
        overall:
          type: object
          properties:
            assigned:  { type: integer }
            completed: { type: integer }
            overdue:   { type: integer }
            rate:      { type: number, format: float }
        users:
          type: array
          items:
            type: object
            properties:
              userId:       { $ref: '#/components/schemas/Uuid' }
              displayName:  { type: string }
              email:        { type: string, format: email }
              status:       { type: string }
              completionPct:{ type: number, format: float }
              completedAt:  { type: string, format: date-time, nullable: true }
              daysOverdue:  { type: integer }
```

---

## 10. Revenue Worker — Instructor Earnings API

**Base URL:** `/api/revenue` | **Port:** 5309 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS Revenue Worker API
  version: 1.0.0
  description: >
    Calculates and tracks instructor revenue shares.
    Stripe Connect payouts triggered automatically at month-end.
    Instructors view their earnings; platform admin views all.

servers:
  - url: /api/revenue

tags:
  - name: Earnings
  - name: Payouts
  - name: Config

paths:

  /earnings/me:
    get:
      tags: [Earnings]
      summary: Get instructor earnings summary
      operationId: getMyEarnings
      parameters:
        - { name: from, in: query, schema: { type: string, format: date } }
        - { name: to,   in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Earnings summary
          content:
            application/json:
              schema: { $ref: '#/components/schemas/EarningsSummary' }

  /earnings/me/courses:
    get:
      tags: [Earnings]
      summary: Per-course earnings breakdown
      operationId: getMyEarningsByCourse
      responses:
        '200':
          description: Per-course earnings
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/CourseEarnings' }

  /payouts/me:
    get:
      tags: [Payouts]
      summary: Payout history for instructor
      operationId: getMyPayouts
      parameters:
        - { name: page, in: query, schema: { type: integer, default: 1 } }
      responses:
        '200':
          description: Payout history
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/Payout' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /config/share-rates:
    put:
      tags: [Config]
      summary: Set revenue share rates (platform admin)
      operationId: setShareRates
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [instructorPercent, platformPercent]
              properties:
                instructorPercent: { type: number, format: float, minimum: 0, maximum: 100 }
                platformPercent:   { type: number, format: float, minimum: 0, maximum: 100 }
                tenantId:          { $ref: '#/components/schemas/Uuid', nullable: true }
      responses:
        '200': { description: Rates updated }

  /admin/earnings:
    get:
      tags: [Earnings]
      summary: All instructor earnings (platform admin)
      operationId: getAllEarnings
      parameters:
        - { name: from, in: query, schema: { type: string, format: date } }
        - { name: to,   in: query, schema: { type: string, format: date } }
        - { name: page, in: query, schema: { type: integer, default: 1 } }
      responses:
        '200':
          description: All earnings
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/EarningsSummary' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

components:
  schemas:
    EarningsSummary:
      type: object
      properties:
        instructorId:  { $ref: '#/components/schemas/Uuid' }
        displayName:   { type: string }
        totalEarned:   { type: integer, description: Cents }
        pendingPayout: { type: integer, description: Cents }
        currency:      { type: string }
        enrollmentCount: { type: integer }
        period:
          type: object
          properties:
            from: { type: string, format: date }
            to:   { type: string, format: date }

    CourseEarnings:
      type: object
      properties:
        courseId:         { $ref: '#/components/schemas/Uuid' }
        courseTitle:      { type: string }
        grossRevenue:     { type: integer }
        instructorShare:  { type: integer }
        platformFee:      { type: integer }
        enrollmentCount:  { type: integer }
        refundedAmount:   { type: integer }

    Payout:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        amount:        { type: integer, description: Cents }
        currency:      { type: string }
        status:        { type: string, enum: [pending, processing, paid, failed] }
        stripePayout:  { type: string, nullable: true }
        periodFrom:    { type: string, format: date }
        periodTo:      { type: string, format: date }
        createdAt:     { type: string, format: date-time }
        paidAt:        { type: string, format: date-time, nullable: true }
```

---

*Document: LMS Phase 3 OpenAPI Specifications · Version 1.0 · April 2026*
*Next: Phase 4 OpenAPI Specifications (Virtual Labs · Digital Credentials · Engagement AI · AI Translation)*
