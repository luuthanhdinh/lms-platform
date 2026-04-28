# LMS Platform — Phase 1 OpenAPI Specifications

**Phase:** 1 — Foundation & Core
**OpenAPI version:** 3.1.0
**Services:** Identity · Course · Content · Enrollment · Progress · Assessment · Certificate
**Auth:** All protected endpoints require `Authorization: Bearer {JWT}` (Keycloak-issued).
**Gateway headers injected per request:** `X-User-Id` · `X-Tenant-Id` · `X-Roles`

---

## Shared Components

```yaml
# Referenced by all service specs as $ref imports
components:
  schemas:
    Uuid:
      type: string
      format: uuid
      example: 3fa85f64-5717-4562-b3fc-2c963f66afa6

    PaginatedMeta:
      type: object
      required: [page, pageSize, totalCount, totalPages]
      properties:
        page:        { type: integer, example: 1 }
        pageSize:    { type: integer, example: 20 }
        totalCount:  { type: integer, example: 243 }
        totalPages:  { type: integer, example: 13 }

    ErrorResponse:
      type: object
      required: [code, message, traceId]
      properties:
        code:    { type: string, example: VALIDATION_ERROR }
        message: { type: string }
        details:
          type: array
          items:
            type: object
            properties:
              field:   { type: string }
              message: { type: string }
        traceId: { type: string }

  responses:
    BadRequest:
      description: Validation error
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ErrorResponse' }
    Unauthorized:  { description: Missing or invalid JWT }
    Forbidden:     { description: Insufficient role }
    NotFound:      { description: Resource not found }
    Conflict:      { description: State conflict or duplicate }

  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: Keycloak-issued JWT; validated at YARP gateway.

security:
  - BearerAuth: []
```

---

## 1. Identity Service

**Base URL:** `/api/identity` | **Port:** 5101 | **DB:** `lms_identity` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Identity Service
  version: 1.0.0
  description: >
    Manages user profiles and tenant configuration.
    Authentication is delegated to Keycloak; this service stores
    supplementary profile data, tenant settings, and user administration.

servers:
  - url: /api/identity

tags:
  - name: Profile
  - name: Users
  - name: Tenant

paths:

  /profile/me:
    get:
      tags: [Profile]
      summary: Get current user profile
      operationId: getMyProfile
      responses:
        '200':
          description: Current user profile
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UserProfile' }
        '401': { $ref: '#/components/responses/Unauthorized' }

    put:
      tags: [Profile]
      summary: Update current user profile
      operationId: updateMyProfile
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateProfileRequest' }
      responses:
        '200':
          description: Updated profile
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UserProfile' }
        '400': { $ref: '#/components/responses/BadRequest' }

  /profile:
    post:
      tags: [Profile]
      summary: Upsert profile on first login (idempotent)
      operationId: upsertProfile
      description: Called by gateway automation; not for direct client use.
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpsertProfileRequest' }
      responses:
        '200':
          description: Profile created or updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UserProfile' }

  /users:
    get:
      tags: [Users]
      summary: List users in tenant (org-admin/admin)
      operationId: listUsers
      parameters:
        - { name: page,     in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize, in: query, schema: { type: integer, default: 20, maximum: 100 } }
        - { name: search,   in: query, schema: { type: string }, description: Filter by name or email }
        - { name: role,     in: query, schema: { type: string, enum: [student, instructor, admin, org-admin] } }
        - { name: isActive, in: query, schema: { type: boolean } }
      responses:
        '200':
          description: Paginated user list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/UserSummary' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }
        '403': { $ref: '#/components/responses/Forbidden' }

  /users/invite:
    post:
      tags: [Users]
      summary: Invite a user to the tenant (org-admin/admin)
      operationId: inviteUser
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/InviteUserRequest' }
      responses:
        '202':
          description: Invite dispatched
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UserInvite' }
        '400': { $ref: '#/components/responses/BadRequest' }
        '409': { $ref: '#/components/responses/Conflict' }

  /users/{userId}/role:
    patch:
      tags: [Users]
      summary: Change user role (org-admin/admin)
      operationId: changeUserRole
      parameters:
        - { name: userId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [role]
              properties:
                role: { type: string, enum: [student, instructor, org-admin] }
      responses:
        '204': { description: Role updated }
        '404': { $ref: '#/components/responses/NotFound' }

  /users/{userId}/deactivate:
    patch:
      tags: [Users]
      summary: Deactivate a user (org-admin/admin)
      operationId: deactivateUser
      parameters:
        - { name: userId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: User deactivated }
        '404': { $ref: '#/components/responses/NotFound' }

  /tenant:
    get:
      tags: [Tenant]
      summary: Get tenant configuration
      operationId: getTenant
      responses:
        '200':
          description: Tenant config
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TenantConfig' }

    put:
      tags: [Tenant]
      summary: Update tenant configuration (org-admin/admin)
      operationId: updateTenant
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateTenantRequest' }
      responses:
        '200':
          description: Updated config
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TenantConfig' }

components:
  schemas:
    UserProfile:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        keycloakId:  { type: string }
        tenantId:    { $ref: '#/components/schemas/Uuid' }
        displayName: { type: string, example: Nguyen Van A }
        email:       { type: string, format: email }
        avatarUrl:   { type: string, format: uri, nullable: true }
        bio:         { type: string, maxLength: 500, nullable: true }
        timezone:    { type: string, example: Asia/Ho_Chi_Minh }
        language:    { type: string, example: vi }
        role:        { type: string, enum: [student, instructor, admin, org-admin] }
        isActive:    { type: boolean }
        createdAt:   { type: string, format: date-time }

    UpdateProfileRequest:
      type: object
      properties:
        displayName: { type: string, minLength: 1, maxLength: 100 }
        avatarUrl:   { type: string, format: uri, nullable: true }
        bio:         { type: string, maxLength: 500, nullable: true }
        timezone:    { type: string }
        language:    { type: string }

    UpsertProfileRequest:
      type: object
      required: [keycloakId, email, tenantId, role]
      properties:
        keycloakId:  { type: string }
        email:       { type: string, format: email }
        displayName: { type: string }
        tenantId:    { $ref: '#/components/schemas/Uuid' }
        role:        { type: string }

    UserSummary:
      type: object
      properties:
        id:          { $ref: '#/components/schemas/Uuid' }
        displayName: { type: string }
        email:       { type: string, format: email }
        role:        { type: string }
        isActive:    { type: boolean }
        createdAt:   { type: string, format: date-time }

    InviteUserRequest:
      type: object
      required: [email, role]
      properties:
        email: { type: string, format: email }
        role:  { type: string, enum: [student, instructor, org-admin] }

    UserInvite:
      type: object
      properties:
        id:        { $ref: '#/components/schemas/Uuid' }
        email:     { type: string }
        role:      { type: string }
        expiresAt: { type: string, format: date-time }

    TenantConfig:
      type: object
      properties:
        id:                 { $ref: '#/components/schemas/Uuid' }
        name:               { type: string }
        logoUrl:            { type: string, format: uri, nullable: true }
        timezone:           { type: string }
        allowedEmailDomains:
          type: array
          items: { type: string }
        plan:               { type: string, enum: [free, pro, enterprise] }

    UpdateTenantRequest:
      type: object
      properties:
        name:    { type: string }
        logoUrl: { type: string, format: uri, nullable: true }
        timezone: { type: string }
        allowedEmailDomains:
          type: array
          items: { type: string }
```

---

## 2. Course Service

**Base URL:** `/api/courses` | **Port:** 5102 | **DB:** `lms_courses` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Course Service
  version: 1.0.0
  description: >
    Manages the course hierarchy: Course → Section → Lesson.
    Does not serve media (ContentService handles that).
    GET catalogue endpoints are public — no auth required.

servers:
  - url: /api/courses

tags:
  - name: Catalogue
    description: Public course discovery (no auth)
  - name: Authoring
    description: Instructor course management
  - name: Structure
    description: Section and lesson CRUD

paths:

  /:
    get:
      tags: [Catalogue]
      summary: Browse course catalogue
      operationId: listCourses
      security: []
      parameters:
        - { name: category,   in: query, schema: { type: string } }
        - { name: tags,       in: query, schema: { type: array, items: { type: string } }, style: form, explode: true }
        - { name: difficulty, in: query, schema: { type: string, enum: [beginner, intermediate, advanced] } }
        - { name: language,   in: query, schema: { type: string } }
        - { name: sort,       in: query, schema: { type: string, enum: [newest, popular, rating], default: newest } }
        - { name: page,       in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize,   in: query, schema: { type: integer, default: 20, maximum: 50 } }
      responses:
        '200':
          description: Paginated catalogue
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/CourseSummary' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

    post:
      tags: [Authoring]
      summary: Create draft course (instructor/admin)
      operationId: createCourse
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateCourseRequest' }
      responses:
        '201':
          description: Course created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Course' }

  /{courseId}:
    get:
      tags: [Catalogue]
      summary: Get course detail with sections
      operationId: getCourse
      security: []
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      responses:
        '200':
          description: Course detail
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseDetail' }
        '404': { $ref: '#/components/responses/NotFound' }

    put:
      tags: [Authoring]
      summary: Update course metadata (instructor/admin)
      operationId: updateCourse
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateCourseRequest' }
      responses:
        '200':
          description: Updated course
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Course' }

  /{courseId}/publish:
    post:
      tags: [Authoring]
      summary: Publish course (instructor/admin)
      operationId: publishCourse
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      responses:
        '200':
          description: Published
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Course' }
        '409': { description: No published lessons exist }

  /{courseId}/unpublish:
    post:
      tags: [Authoring]
      summary: Unpublish course back to Draft
      operationId: unpublishCourse
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      responses:
        '200':
          description: Unpublished
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Course' }

  /{courseId}/duplicate:
    post:
      tags: [Authoring]
      summary: Deep-copy course structure (not media)
      operationId: duplicateCourse
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      responses:
        '201':
          description: New draft course
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Course' }

  /{courseId}/syllabus:
    get:
      tags: [Catalogue]
      summary: Full syllabus with durations
      operationId: getCourseSyllabus
      security: []
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      responses:
        '200':
          description: Syllabus
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseSyllabus' }

  /{courseId}/sections:
    post:
      tags: [Structure]
      summary: Add section (instructor/admin)
      operationId: createSection
      parameters:
        - { $ref: '#/components/parameters/courseId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SectionRequest' }
      responses:
        '201':
          description: Section created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseSection' }

  /{courseId}/sections/{sectionId}:
    put:
      tags: [Structure]
      summary: Update section title/order
      operationId: updateSection
      parameters:
        - { $ref: '#/components/parameters/courseId' }
        - { $ref: '#/components/parameters/sectionId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SectionRequest' }
      responses:
        '200':
          description: Updated section
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseSection' }

    delete:
      tags: [Structure]
      summary: Delete section (fails if lessons exist)
      operationId: deleteSection
      parameters:
        - { $ref: '#/components/parameters/courseId' }
        - { $ref: '#/components/parameters/sectionId' }
      responses:
        '204': { description: Deleted }
        '409': { description: Section has lessons }

  /{courseId}/sections/{sectionId}/lessons:
    post:
      tags: [Structure]
      summary: Add lesson to section
      operationId: createLesson
      parameters:
        - { $ref: '#/components/parameters/courseId' }
        - { $ref: '#/components/parameters/sectionId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateLessonRequest' }
      responses:
        '201':
          description: Lesson created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseLesson' }

  /{courseId}/sections/{sectionId}/lessons/{lessonId}:
    put:
      tags: [Structure]
      summary: Update lesson metadata
      operationId: updateLesson
      parameters:
        - { $ref: '#/components/parameters/courseId' }
        - { $ref: '#/components/parameters/sectionId' }
        - { name: lessonId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateLessonRequest' }
      responses:
        '200':
          description: Updated lesson
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseLesson' }

    delete:
      tags: [Structure]
      summary: Remove lesson
      operationId: deleteLesson
      parameters:
        - { $ref: '#/components/parameters/courseId' }
        - { $ref: '#/components/parameters/sectionId' }
        - { name: lessonId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '204': { description: Deleted }

components:
  parameters:
    courseId:
      name: courseId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }
    sectionId:
      name: sectionId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CourseSummary:
      type: object
      properties:
        id:                  { $ref: '#/components/schemas/Uuid' }
        title:               { type: string }
        description:         { type: string }
        thumbnailUrl:        { type: string, format: uri, nullable: true }
        category:            { type: string }
        tags:                { type: array, items: { type: string } }
        difficulty:          { type: string, enum: [beginner, intermediate, advanced] }
        language:            { type: string }
        instructorName:      { type: string }
        enrollmentCount:     { type: integer }
        lessonCount:         { type: integer }
        totalDurationSeconds:{ type: integer }
        status:              { type: string, enum: [draft, published] }
        publishedAt:         { type: string, format: date-time, nullable: true }

    Course:
      allOf:
        - { $ref: '#/components/schemas/CourseSummary' }
        - type: object
          properties:
            tenantId:     { $ref: '#/components/schemas/Uuid' }
            instructorId: { $ref: '#/components/schemas/Uuid' }
            version:      { type: integer }
            createdAt:    { type: string, format: date-time }

    CourseDetail:
      allOf:
        - { $ref: '#/components/schemas/Course' }
        - type: object
          properties:
            sections:
              type: array
              items: { $ref: '#/components/schemas/SectionWithLessons' }
            prerequisites:
              type: array
              items:
                type: object
                properties:
                  courseId:    { $ref: '#/components/schemas/Uuid' }
                  courseTitle: { type: string }

    CourseSyllabus:
      type: object
      properties:
        courseId:             { $ref: '#/components/schemas/Uuid' }
        totalDurationSeconds: { type: integer }
        totalLessons:         { type: integer }
        sections:
          type: array
          items: { $ref: '#/components/schemas/SectionWithLessons' }

    SectionWithLessons:
      type: object
      properties:
        id:    { $ref: '#/components/schemas/Uuid' }
        title: { type: string }
        order: { type: integer }
        lessons:
          type: array
          items: { $ref: '#/components/schemas/LessonSummary' }

    LessonSummary:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        title:           { type: string }
        durationSeconds: { type: integer, nullable: true }
        isFreePreview:   { type: boolean }
        isOptional:      { type: boolean }
        contentType:     { type: string, enum: [video, pdf, scorm, h5p] }
        order:           { type: integer }

    CourseSection:
      type: object
      properties:
        id:       { $ref: '#/components/schemas/Uuid' }
        courseId: { $ref: '#/components/schemas/Uuid' }
        title:    { type: string }
        order:    { type: integer }

    CourseLesson:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        sectionId:       { $ref: '#/components/schemas/Uuid' }
        courseId:        { $ref: '#/components/schemas/Uuid' }
        title:           { type: string }
        contentItemId:   { $ref: '#/components/schemas/Uuid' }
        durationSeconds: { type: integer, nullable: true }
        isFreePreview:   { type: boolean }
        isOptional:      { type: boolean }
        order:           { type: integer }

    CreateCourseRequest:
      type: object
      required: [title, category, difficulty, language]
      properties:
        title:                 { type: string, minLength: 3, maxLength: 200 }
        description:           { type: string, maxLength: 2000 }
        thumbnailContentId:    { $ref: '#/components/schemas/Uuid', nullable: true }
        category:              { type: string }
        tags:                  { type: array, items: { type: string }, maxItems: 10 }
        difficulty:            { type: string, enum: [beginner, intermediate, advanced] }
        language:              { type: string }
        prerequisiteCourseIds: { type: array, items: { $ref: '#/components/schemas/Uuid' } }

    UpdateCourseRequest:
      type: object
      properties:
        title:              { type: string }
        description:        { type: string }
        thumbnailContentId: { $ref: '#/components/schemas/Uuid', nullable: true }
        category:           { type: string }
        tags:               { type: array, items: { type: string } }
        difficulty:         { type: string, enum: [beginner, intermediate, advanced] }
        language:           { type: string }

    SectionRequest:
      type: object
      required: [title]
      properties:
        title: { type: string, maxLength: 200 }
        order: { type: integer, minimum: 0 }

    CreateLessonRequest:
      type: object
      required: [title, contentItemId]
      properties:
        title:         { type: string, maxLength: 200 }
        contentItemId: { $ref: '#/components/schemas/Uuid' }
        isFreePreview: { type: boolean, default: false }
        isOptional:    { type: boolean, default: false }
        order:         { type: integer, minimum: 0 }

    UpdateLessonRequest:
      type: object
      properties:
        title:         { type: string, maxLength: 200 }
        isFreePreview: { type: boolean }
        isOptional:    { type: boolean }
        order:         { type: integer, minimum: 0 }
```

---

## 3. Content Delivery Service

**Base URL:** `/api/content` | **Port:** 5103 | **DB:** `lms_content` (MongoDB) + S3

```yaml
openapi: 3.1.0
info:
  title: LMS Content Delivery Service
  version: 1.0.0
  description: >
    Manages media assets (video, PDF, SCORM, H5P, images).
    Metadata in MongoDB; binaries in S3.
    Videos transcoded to HLS adaptive bitrate.
    Stream/download endpoints return signed URLs (15-min TTL).

servers:
  - url: /api/content

tags:
  - name: Upload
  - name: Delivery
  - name: Playback

paths:

  /upload:
    post:
      tags: [Upload]
      summary: Request pre-signed S3 upload URL
      operationId: requestUploadUrl
      description: Client uploads directly to S3; then calls /{id}/process.
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UploadRequest' }
      responses:
        '200':
          description: Pre-signed URL + content item ID
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UploadUrlResponse' }
        '400': { $ref: '#/components/responses/BadRequest' }

  /{contentItemId}:
    get:
      tags: [Upload]
      summary: Get content item metadata and status
      operationId: getContentItem
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '200':
          description: Content item
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ContentItem' }
        '404': { $ref: '#/components/responses/NotFound' }

    delete:
      tags: [Upload]
      summary: Delete content item (instructor/admin)
      operationId: deleteContentItem
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '204': { description: Marked for deletion }
        '409': { description: Referenced by one or more lessons }

  /{contentItemId}/process:
    post:
      tags: [Upload]
      summary: Trigger async transcoding/processing
      operationId: processContent
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '202': { description: Job queued }
        '409': { description: Already processed or in progress }

  /{contentItemId}/stream:
    get:
      tags: [Delivery]
      summary: Get signed HLS manifest URL (requires enrollment)
      operationId: getStreamUrl
      description: TTL = 15 minutes. Checks enrollment via EnrollmentService.
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '200':
          description: Signed stream URL
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StreamUrlResponse' }
        '403': { description: Not enrolled }
        '422': { description: Not yet processed }

  /{contentItemId}/stream/refresh:
    post:
      tags: [Delivery]
      summary: Refresh expired signed stream URL
      operationId: refreshStreamUrl
      description: Called by video player when URL TTL expires mid-playback.
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '200':
          description: New signed URL
          content:
            application/json:
              schema: { $ref: '#/components/schemas/StreamUrlResponse' }
        '403': { description: Enrollment no longer active }

  /{contentItemId}/download:
    get:
      tags: [Delivery]
      summary: Get signed download URL for PDF/SCORM (requires enrollment)
      operationId: getDownloadUrl
      description: TTL = 5 minutes.
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '200':
          description: Signed download URL
          content:
            application/json:
              schema: { $ref: '#/components/schemas/DownloadUrlResponse' }
        '403': { description: Not enrolled }

  /{contentItemId}/progress:
    post:
      tags: [Playback]
      summary: Report video playback position (every 30s)
      operationId: reportPlaybackProgress
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/PlaybackProgressRequest' }
      responses:
        '204': { description: Progress recorded }

  /{contentItemId}/stream/heartbeat:
    post:
      tags: [Playback]
      summary: Player heartbeat to refresh device session (Phase 3 DRM)
      operationId: streamHeartbeat
      description: >
        Called every 5 minutes by player. Refreshes the active device
        session TTL in Redis. Required for Phase 3 DRM device-limit enforcement.
      parameters:
        - { $ref: '#/components/parameters/contentItemId' }
      responses:
        '204': { description: Session refreshed }
        '403': { description: Device session limit exceeded }

components:
  parameters:
    contentItemId:
      name: contentItemId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    UploadRequest:
      type: object
      required: [filename, contentType, sizeBytes]
      properties:
        filename:    { type: string, example: intro-aspire.mp4 }
        contentType:
          type: string
          enum: [video/mp4, video/webm, application/pdf,
                 application/zip, image/jpeg, image/png, image/webp]
        sizeBytes:
          type: integer
          format: int64
          maximum: 4294967296

    UploadUrlResponse:
      type: object
      properties:
        contentItemId:       { $ref: '#/components/schemas/Uuid' }
        uploadUrl:           { type: string, format: uri }
        uploadUrlExpiresAt:  { type: string, format: date-time }

    ContentItem:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        tenantId:        { $ref: '#/components/schemas/Uuid' }
        uploadedBy:      { $ref: '#/components/schemas/Uuid' }
        filename:        { type: string }
        type:            { type: string, enum: [video, pdf, scorm, h5p, image] }
        status:          { type: string, enum: [pending, processing, ready, failed] }
        durationSeconds: { type: integer, nullable: true }
        thumbnailUrl:    { type: string, format: uri, nullable: true }
        hlsManifestUrl:  { type: string, format: uri, nullable: true }
        captionVttUrl:   { type: string, format: uri, nullable: true }
        captionLanguage: { type: string, nullable: true }
        sizeBytes:       { type: integer, format: int64 }
        createdAt:       { type: string, format: date-time }

    StreamUrlResponse:
      type: object
      properties:
        signedManifestUrl:     { type: string, format: uri }
        expiresAt:             { type: string, format: date-time }
        resumePositionSeconds: { type: integer }

    DownloadUrlResponse:
      type: object
      properties:
        signedDownloadUrl: { type: string, format: uri }
        expiresAt:         { type: string, format: date-time }
        filename:          { type: string }

    PlaybackProgressRequest:
      type: object
      required: [positionSeconds, totalSeconds, lessonId]
      properties:
        positionSeconds: { type: integer, minimum: 0 }
        totalSeconds:    { type: integer, minimum: 1 }
        lessonId:        { $ref: '#/components/schemas/Uuid' }
```

---

## 4. Enrollment Service

**Base URL:** `/api/enrollments` | **Port:** 5104 | **DB:** `lms_enrollment` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Enrollment Service
  version: 1.0.0
  description: >
    Manages course enrollment lifecycle: seat limits, waitlist,
    prerequisite validation, and status transitions.
    PaymentService integration stubbed in Phase 1 (isFree flag).

servers:
  - url: /api/enrollments

tags:
  - name: Enrollment
  - name: Waitlist
  - name: Admin

paths:

  /:
    post:
      tags: [Enrollment]
      summary: Enroll in a course
      operationId: enroll
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/EnrollRequest' }
      responses:
        '201':
          description: Enrolled
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Enrollment' }
        '409':
          description: Already enrolled / prerequisite not met / course full / payment required
          content:
            application/json:
              schema:
                allOf:
                  - { $ref: '#/components/schemas/ErrorResponse' }
                  - type: object
                    properties:
                      code:
                        type: string
                        enum: [ALREADY_ENROLLED, PREREQUISITE_NOT_MET,
                               COURSE_FULL, PAYMENT_REQUIRED]

  /me:
    get:
      tags: [Enrollment]
      summary: Get current user's enrollments
      operationId: getMyEnrollments
      parameters:
        - { name: status, in: query, schema: { type: string, enum: [pending, active, completed, suspended] } }
      responses:
        '200':
          description: Enrollment list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/EnrollmentSummary' } }

  /{enrollmentId}:
    get:
      tags: [Enrollment]
      summary: Get enrollment detail
      operationId: getEnrollment
      parameters:
        - { $ref: '#/components/parameters/enrollmentId' }
      responses:
        '200':
          description: Enrollment
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Enrollment' }
        '404': { $ref: '#/components/responses/NotFound' }

    delete:
      tags: [Admin]
      summary: Revoke enrollment (admin only)
      operationId: revokeEnrollment
      parameters:
        - { $ref: '#/components/parameters/enrollmentId' }
      responses:
        '204': { description: Revoked }

  /check:
    get:
      tags: [Enrollment]
      summary: Check enrollment status — internal service-to-service
      operationId: checkEnrollment
      description: Not exposed through public gateway; used by ContentService and ProgressService.
      parameters:
        - { name: courseId, in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: userId,   in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Status
          content:
            application/json:
              schema:
                type: object
                properties:
                  enrolled:   { type: boolean }
                  status:     { type: string, nullable: true }
                  enrolledAt: { type: string, format: date-time, nullable: true }

  /manual:
    post:
      tags: [Admin]
      summary: Manually enroll a student (instructor/admin)
      operationId: manualEnroll
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/ManualEnrollRequest' }
      responses:
        '201':
          description: Enrolled
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Enrollment' }

  /{courseId}/students:
    get:
      tags: [Admin]
      summary: List enrolled students with progress (instructor/admin)
      operationId: getCourseStudents
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: page, in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize, in: query, schema: { type: integer, default: 50 } }
      responses:
        '200':
          description: Student list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/EnrolledStudent' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /{courseId}/waitlist:
    post:
      tags: [Waitlist]
      summary: Join waitlist
      operationId: joinWaitlist
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '201':
          description: Added to waitlist
          content:
            application/json:
              schema: { $ref: '#/components/schemas/WaitlistEntry' }
        '409': { description: Already on waitlist or enrolled }

components:
  parameters:
    enrollmentId:
      name: enrollmentId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    EnrollRequest:
      type: object
      required: [courseId]
      properties:
        courseId:         { $ref: '#/components/schemas/Uuid' }
        paymentReference: { type: string, nullable: true }

    ManualEnrollRequest:
      type: object
      required: [userId, courseId]
      properties:
        userId:   { $ref: '#/components/schemas/Uuid' }
        courseId: { $ref: '#/components/schemas/Uuid' }
        note:     { type: string, maxLength: 500 }

    Enrollment:
      type: object
      properties:
        id:               { $ref: '#/components/schemas/Uuid' }
        userId:           { $ref: '#/components/schemas/Uuid' }
        courseId:         { $ref: '#/components/schemas/Uuid' }
        tenantId:         { $ref: '#/components/schemas/Uuid' }
        status:           { type: string, enum: [pending, active, completed, suspended] }
        paymentReference: { type: string, nullable: true }
        enrolledAt:       { type: string, format: date-time }
        completedAt:      { type: string, format: date-time, nullable: true }
        expiresAt:        { type: string, format: date-time, nullable: true }

    EnrollmentSummary:
      allOf:
        - { $ref: '#/components/schemas/Enrollment' }
        - type: object
          properties:
            courseTitle:        { type: string }
            courseThumbnailUrl: { type: string, format: uri, nullable: true }
            completionPercent:  { type: number, format: float }
            lastAccessedAt:     { type: string, format: date-time, nullable: true }

    EnrolledStudent:
      type: object
      properties:
        userId:           { $ref: '#/components/schemas/Uuid' }
        displayName:      { type: string }
        email:            { type: string, format: email }
        enrolledAt:       { type: string, format: date-time }
        completionPercent:{ type: number, format: float }
        lastActiveAt:     { type: string, format: date-time, nullable: true }

    WaitlistEntry:
      type: object
      properties:
        id:       { $ref: '#/components/schemas/Uuid' }
        userId:   { $ref: '#/components/schemas/Uuid' }
        courseId: { $ref: '#/components/schemas/Uuid' }
        position: { type: integer }
        joinedAt: { type: string, format: date-time }
```

---

## 5. Progress Service

**Base URL:** `/api/progress` | **Port:** 5105 | **DB:** `lms_progress` (PostgreSQL)

```yaml
openapi: 3.1.0
info:
  title: LMS Progress Service
  version: 1.0.0
  description: >
    Tracks lesson completion, course progress %, and emits xAPI statements.
    Optional lessons excluded from completion % denominator.
    Publishes CourseCompleted when 100% required lessons complete.

servers:
  - url: /api/progress

tags:
  - name: Lessons
  - name: Courses
  - name: Analytics

paths:

  /lessons/{lessonId}/complete:
    post:
      tags: [Lessons]
      summary: Mark lesson complete (idempotent)
      operationId: completeLesson
      description: >
        Recalculates course completion %. Publishes LessonCompleted event.
        Publishes CourseCompleted if 100% required lessons done.
      parameters:
        - { name: lessonId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [courseId, watchPercent]
              properties:
                courseId:     { $ref: '#/components/schemas/Uuid' }
                watchPercent:
                  type: number
                  format: float
                  minimum: 0
                  maximum: 100
      responses:
        '200':
          description: Progress updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LessonProgressResult' }
        '403': { description: Not enrolled }

  /courses/{courseId}:
    get:
      tags: [Courses]
      summary: Get course progress for current user
      operationId: getCourseProgress
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Course progress
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CourseProgress' }

  /courses/{courseId}/lessons:
    get:
      tags: [Courses]
      summary: Per-lesson completion status for current user
      operationId: getCourseLessonProgress
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Per-lesson list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/LessonProgress' } }

  /courses/{courseId}/analytics:
    get:
      tags: [Analytics]
      summary: Per-lesson completion analytics (instructor/admin)
      operationId: getCourseAnalytics
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Analytics
          content:
            application/json:
              schema:
                type: object
                properties:
                  totalEnrolled: { type: integer }
                  lessons:
                    type: array
                    items: { $ref: '#/components/schemas/LessonAnalytics' }

  /me:
    get:
      tags: [Courses]
      summary: Progress across all enrolled courses
      operationId: getMyProgress
      parameters:
        - { name: status, in: query, schema: { type: string, enum: [in_progress, completed, not_started] } }
      responses:
        '200':
          description: Summary list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/CourseProgressSummary' } }

  /sync:
    post:
      tags: [Lessons]
      summary: Batch sync offline progress events (PWA — Phase 3)
      operationId: syncOfflineProgress
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [events]
              properties:
                events:
                  type: array
                  maxItems: 50
                  items: { $ref: '#/components/schemas/OfflineProgressEvent' }
      responses:
        '200':
          description: Sync result
          content:
            application/json:
              schema:
                type: object
                properties:
                  processed: { type: integer }
                  skipped:   { type: integer }
                  failed:    { type: integer }

components:
  schemas:
    LessonProgressResult:
      type: object
      properties:
        lessonProgress:         { $ref: '#/components/schemas/LessonProgress' }
        courseCompletionPercent:{ type: number, format: float }
        courseCompleted:        { type: boolean }

    LessonProgress:
      type: object
      properties:
        lessonId:       { $ref: '#/components/schemas/Uuid' }
        courseId:       { $ref: '#/components/schemas/Uuid' }
        status:         { type: string, enum: [not_started, in_progress, completed] }
        watchPercent:   { type: number, format: float }
        completedAt:    { type: string, format: date-time, nullable: true }
        lastAccessedAt: { type: string, format: date-time, nullable: true }

    CourseProgress:
      type: object
      properties:
        courseId:              { $ref: '#/components/schemas/Uuid' }
        userId:                { $ref: '#/components/schemas/Uuid' }
        completionPercent:     { type: number, format: float }
        lessonsCompleted:      { type: integer }
        totalRequiredLessons:  { type: integer }
        lastAccessedAt:        { type: string, format: date-time, nullable: true }
        completedAt:           { type: string, format: date-time, nullable: true }

    CourseProgressSummary:
      allOf:
        - { $ref: '#/components/schemas/CourseProgress' }
        - type: object
          properties:
            courseTitle:        { type: string }
            courseThumbnailUrl: { type: string, format: uri, nullable: true }

    LessonAnalytics:
      type: object
      properties:
        lessonId:       { $ref: '#/components/schemas/Uuid' }
        lessonTitle:    { type: string }
        startedCount:   { type: integer }
        completedCount: { type: integer }
        completionRate: { type: number, format: float }
        avgWatchPercent:{ type: number, format: float }

    OfflineProgressEvent:
      type: object
      required: [clientEventId, type, payload, clientTimestamp]
      properties:
        clientEventId:   { type: string, format: uuid }
        type:            { type: string, enum: [lesson_completed, playback_progress] }
        payload:         { type: object }
        clientTimestamp: { type: string, format: date-time }
```

---

## 6. Assessment Service

**Base URL:** `/api/assessments` | **Port:** 5106 | **DB:** `lms_assessment` (PostgreSQL) + Redis

```yaml
openapi: 3.1.0
info:
  title: LMS Assessment Service
  version: 1.0.0
  description: >
    Quiz engine with question bank, timed sessions (Redis), and auto-grading.
    Correct answers never returned until after submission.
    Phase 2 adds adaptive IRT engine and LLM grading extensions.

servers:
  - url: /api/assessments

tags:
  - name: Management
  - name: Sessions
  - name: Analytics
  - name: Grading
    description: Phase 2 LLM grading queue (stub endpoints defined here)

paths:

  /:
    post:
      tags: [Management]
      summary: Create assessment (instructor/admin)
      operationId: createAssessment
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateAssessmentRequest' }
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Assessment' }

  /{assessmentId}:
    get:
      tags: [Management]
      summary: Get assessment detail
      operationId: getAssessment
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      responses:
        '200':
          description: Assessment (no correct answers for students)
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Assessment' }

  /{assessmentId}/questions:
    post:
      tags: [Management]
      summary: Add question (instructor/admin)
      operationId: addQuestion
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateQuestionRequest' }
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Question' }

  /{assessmentId}/questions/{questionId}:
    put:
      tags: [Management]
      summary: Update question
      operationId: updateQuestion
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
        - { $ref: '#/components/parameters/questionId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateQuestionRequest' }
      responses:
        '200':
          description: Updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/Question' }

    delete:
      tags: [Management]
      summary: Remove question
      operationId: deleteQuestion
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
        - { $ref: '#/components/parameters/questionId' }
      responses:
        '204': { description: Deleted }

  /{assessmentId}/sessions:
    post:
      tags: [Sessions]
      summary: Start a quiz session
      operationId: startSession
      description: >
        Validates attempt count. Samples questions if randomised.
        Stores session in Redis (TTL = timeLimitSeconds + 60s).
        Returns questions without correct answers.
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      responses:
        '201':
          description: Session started
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AssessmentSession' }
        '403': { description: Max attempts exceeded }
        '404': { $ref: '#/components/responses/NotFound' }

  /sessions/{sessionId}/submit:
    post:
      tags: [Sessions]
      summary: Submit answers and receive graded result
      operationId: submitSession
      description: >
        Auto-grades MCQ/TrueFalse. Publishes AssessmentSubmitted event.
        For short-answer/essay (Phase 2): publishes EssaySubmitted instead.
      parameters:
        - { name: sessionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/SubmitAnswersRequest' }
      responses:
        '200':
          description: Graded result
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AssessmentResult' }
        '404': { description: Session not found or expired }
        '409': { description: Already submitted }

  /{assessmentId}/attempts/me:
    get:
      tags: [Sessions]
      summary: Get attempt history for current user
      operationId: getMyAttempts
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      responses:
        '200':
          description: Attempt history
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/AttemptSummary' } }
                  attemptsRemaining: { type: integer }

  /{assessmentId}/analytics:
    get:
      tags: [Analytics]
      summary: Per-question analytics (instructor/admin)
      operationId: getAssessmentAnalytics
      parameters:
        - { $ref: '#/components/parameters/assessmentId' }
      responses:
        '200':
          description: Analytics
          content:
            application/json:
              schema: { $ref: '#/components/schemas/AssessmentAnalytics' }

  # Phase 2 stubs — grading queue (implemented in Phase 2 Sprint 13-14)
  /grading/queue:
    get:
      tags: [Grading]
      summary: Get pending essay submissions awaiting review (Phase 2)
      operationId: getGradingQueue
      description: Returns submissions with AI-suggested grade pending instructor approval.
      responses:
        '200':
          description: Grading queue
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items: { $ref: '#/components/schemas/GradingQueueItem' }

  /grading/{submissionId}/approve:
    post:
      tags: [Grading]
      summary: Approve AI-suggested grade (Phase 2)
      operationId: approveGrade
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200': { description: Grade released to student }

  /grading/{submissionId}/override:
    post:
      tags: [Grading]
      summary: Override AI grade with instructor score (Phase 2)
      operationId: overrideGrade
      parameters:
        - { name: submissionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [finalScore, feedbackText]
              properties:
                finalScore:   { type: number, format: float }
                feedbackText: { type: string, maxLength: 2000 }
      responses:
        '200': { description: Grade overridden and released }

  /attempts/{attemptId}/appeal:
    post:
      tags: [Grading]
      summary: Submit grade appeal (Phase 2)
      operationId: submitAppeal
      parameters:
        - { name: attemptId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [justification]
              properties:
                justification: { type: string, minLength: 10, maxLength: 2000 }
      responses:
        '201': { description: Appeal submitted }
        '409': { description: Already appealed or deadline passed }

components:
  parameters:
    assessmentId:
      name: assessmentId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }
    questionId:
      name: questionId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CreateAssessmentRequest:
      type: object
      required: [courseId, title, passingScore]
      properties:
        courseId:           { $ref: '#/components/schemas/Uuid' }
        lessonId:           { $ref: '#/components/schemas/Uuid', nullable: true }
        title:              { type: string, maxLength: 200 }
        passingScore:       { type: number, format: float, minimum: 0, maximum: 100 }
        timeLimitSeconds:   { type: integer, nullable: true, minimum: 30 }
        maxAttempts:        { type: integer, default: 3, minimum: 1 }
        isRandomised:       { type: boolean, default: false }
        questionSampleSize: { type: integer, nullable: true, minimum: 1 }
        isAdaptive:         { type: boolean, default: false, description: Phase 2 adaptive engine }

    Assessment:
      type: object
      properties:
        id:                 { $ref: '#/components/schemas/Uuid' }
        courseId:           { $ref: '#/components/schemas/Uuid' }
        lessonId:           { $ref: '#/components/schemas/Uuid', nullable: true }
        tenantId:           { $ref: '#/components/schemas/Uuid' }
        title:              { type: string }
        passingScore:       { type: number, format: float }
        timeLimitSeconds:   { type: integer, nullable: true }
        maxAttempts:        { type: integer }
        isRandomised:       { type: boolean }
        questionSampleSize: { type: integer, nullable: true }
        questionCount:      { type: integer }
        isAdaptive:         { type: boolean }

    CreateQuestionRequest:
      type: object
      required: [type, prompt, options, correctOptionIndex, points]
      properties:
        type:
          type: string
          enum: [mcq, true_false, short_answer, essay]
          description: short_answer and essay graded by LLM in Phase 2
        prompt:              { type: string, maxLength: 1000 }
        options:
          type: array
          minItems: 2
          maxItems: 6
          items:
            type: object
            required: [text]
            properties:
              text: { type: string }
        correctOptionIndex:  { type: integer, minimum: 0 }
        explanation:         { type: string, maxLength: 500, nullable: true }
        points:              { type: integer, minimum: 1, default: 1 }
        difficultyRating:
          type: number
          format: float
          minimum: 1.0
          maximum: 5.0
          description: Used by Phase 2 adaptive IRT engine
        rubric:
          type: array
          description: Required for short_answer/essay (Phase 2)
          nullable: true
          items:
            type: object
            properties:
              name:      { type: string }
              maxPoints: { type: integer }

    Question:
      allOf:
        - { $ref: '#/components/schemas/CreateQuestionRequest' }
        - type: object
          properties:
            id:           { $ref: '#/components/schemas/Uuid' }
            assessmentId: { $ref: '#/components/schemas/Uuid' }
            order:        { type: integer }

    UpdateQuestionRequest:
      type: object
      properties:
        prompt:             { type: string }
        options:            { type: array, items: { type: object, properties: { text: { type: string } } } }
        correctOptionIndex: { type: integer }
        explanation:        { type: string, nullable: true }
        points:             { type: integer }
        difficultyRating:   { type: number, format: float }

    AssessmentSession:
      type: object
      properties:
        sessionId:        { $ref: '#/components/schemas/Uuid' }
        assessmentId:     { $ref: '#/components/schemas/Uuid' }
        questions:
          type: array
          description: No correctOptionIndex exposed
          items:
            type: object
            properties:
              id:      { $ref: '#/components/schemas/Uuid' }
              type:    { type: string }
              prompt:  { type: string }
              options:
                type: array
                items:
                  type: object
                  properties:
                    index: { type: integer }
                    text:  { type: string }
              points:  { type: integer }
        startedAt:        { type: string, format: date-time }
        expiresAt:        { type: string, format: date-time, nullable: true }
        timeLimitSeconds: { type: integer, nullable: true }
        # Phase 2 adaptive fields (null in Phase 1)
        isAdaptive:       { type: boolean }
        currentTheta:     { type: number, format: float, nullable: true }

    SubmitAnswersRequest:
      type: object
      required: [answers]
      properties:
        answers:
          type: array
          items:
            type: object
            required: [questionId]
            properties:
              questionId:          { $ref: '#/components/schemas/Uuid' }
              selectedOptionIndex: { type: integer, minimum: 0, nullable: true }
              textAnswer:          { type: string, nullable: true, description: For short_answer/essay }

    AssessmentResult:
      type: object
      properties:
        attemptId:    { $ref: '#/components/schemas/Uuid' }
        score:        { type: number, format: float }
        maxScore:     { type: number, format: float }
        percentage:   { type: number, format: float }
        passed:       { type: boolean }
        timeTakenSeconds: { type: integer }
        gradingStatus:
          type: string
          enum: [auto_graded, pending_review]
          description: pending_review for essay/short_answer (Phase 2)
        questionResults:
          type: array
          items:
            type: object
            properties:
              questionId:          { $ref: '#/components/schemas/Uuid' }
              correct:             { type: boolean, nullable: true }
              selectedOptionIndex: { type: integer, nullable: true }
              correctOptionIndex:  { type: integer, nullable: true }
              explanation:         { type: string, nullable: true }
              pointsAwarded:       { type: integer }

    AttemptSummary:
      type: object
      properties:
        id:            { $ref: '#/components/schemas/Uuid' }
        score:         { type: number, format: float }
        percentage:    { type: number, format: float }
        passed:        { type: boolean }
        startedAt:     { type: string, format: date-time }
        submittedAt:   { type: string, format: date-time }

    AssessmentAnalytics:
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

    GradingQueueItem:
      type: object
      properties:
        submissionId:     { $ref: '#/components/schemas/Uuid' }
        studentName:      { type: string }
        assessmentTitle:  { type: string }
        courseTitle:      { type: string }
        submittedAt:      { type: string, format: date-time }
        suggestedScore:   { type: number, format: float }
        feedbackText:     { type: string }
```

---

## 7. Certificate Service

**Base URL:** `/api/certificates` + `/verify` | **Port:** 5107 | **DB:** PostgreSQL + S3

```yaml
openapi: 3.1.0
info:
  title: LMS Certificate Service
  version: 1.0.0
  description: >
    Issues PDF certificates on CourseCompleted events.
    Phase 1: PDF + verification URL.
    Phase 4: Upgraded to Open Badges 3.0 + optional blockchain anchoring.

servers:
  - url: /

tags:
  - name: Certificates
  - name: Verification
    description: Public — no auth required

paths:

  /api/certificates/me:
    get:
      tags: [Certificates]
      summary: Get all certificates for current user
      operationId: getMyCertificates
      responses:
        '200':
          description: Certificate list
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/CertificateSummary' } }

  /api/certificates/{certificateId}/download:
    get:
      tags: [Certificates]
      summary: Get signed PDF download URL (TTL 1 hour)
      operationId: downloadCertificate
      parameters:
        - { name: certificateId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Signed URL
          content:
            application/json:
              schema:
                type: object
                properties:
                  downloadUrl: { type: string, format: uri }
                  expiresAt:   { type: string, format: date-time }
        '404': { $ref: '#/components/responses/NotFound' }

  # Phase 4 additions (stub schemas defined now)
  /api/certificates/{certificateId}/share:
    get:
      tags: [Certificates]
      summary: Get shareable OG image card URL (Phase 4)
      operationId: getCertificateShareCard
      parameters:
        - { name: certificateId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Share card URL
          content:
            application/json:
              schema:
                type: object
                properties:
                  shareCardUrl:    { type: string, format: uri }
                  linkedInShareUrl:{ type: string, format: uri }

  /api/certificates/wallet:
    get:
      tags: [Certificates]
      summary: Full credential wallet (Phase 4 — OB3)
      operationId: getWallet
      responses:
        '200':
          description: Wallet
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/WalletItem' } }

  /verify/{verificationCode}:
    get:
      tags: [Verification]
      summary: Verify a certificate — public, no auth
      operationId: verifyCertificate
      security: []
      description: >
        Returns HTML for browser requests (Accept: text/html).
        Returns JSON for API requests (Accept: application/json).
      parameters:
        - { name: verificationCode, in: path, required: true, schema: { type: string, format: uuid } }
      responses:
        '200':
          description: Verification result
          content:
            application/json:
              schema: { $ref: '#/components/schemas/VerificationResult' }
            text/html:
              schema: { type: string }
        '404': { description: Not found }

components:
  schemas:
    CertificateSummary:
      type: object
      properties:
        id:               { $ref: '#/components/schemas/Uuid' }
        courseId:         { $ref: '#/components/schemas/Uuid' }
        courseTitle:      { type: string }
        verificationCode: { type: string, format: uuid }
        verifyUrl:        { type: string, format: uri }
        issuedAt:         { type: string, format: date-time }
        revokedAt:        { type: string, format: date-time, nullable: true }

    VerificationResult:
      type: object
      properties:
        valid:             { type: boolean }
        studentName:       { type: string, nullable: true }
        courseTitle:       { type: string, nullable: true }
        issuerName:        { type: string, nullable: true }
        completedAt:       { type: string, format: date-time, nullable: true }
        issuedAt:          { type: string, format: date-time, nullable: true }
        revokedAt:         { type: string, format: date-time, nullable: true }
        revocationReason:  { type: string, nullable: true }
        # Phase 4 blockchain fields
        blockchainAnchor:
          type: object
          nullable: true
          properties:
            txHash:      { type: string }
            network:     { type: string }
            explorerUrl: { type: string, format: uri }
            anchoredAt:  { type: string, format: date-time }

    WalletItem:
      allOf:
        - { $ref: '#/components/schemas/CertificateSummary' }
        - type: object
          properties:
            credentialJwt:    { type: string, description: OB3 JWT (Phase 4) }
            linkedInShareUrl: { type: string, format: uri }
            pdfDownloadUrl:   { type: string, format: uri }
            anchorStatus:     { type: string, enum: [not_anchored, pending, confirmed, failed] }
```

---

## Implementation Notes

### Swashbuckle setup per service

```csharp
// Program.cs in each service
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo {
        Title = "LMS [ServiceName]",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Keycloak-issued JWT. Validated at YARP gateway."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { ... });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

// Aspire AppHost — expose Swagger UI per service
var courseApi = builder.AddProject<Projects.LMS_CourseService>("course-api");
// Swagger UI auto-registered at /swagger on each service port
```

### Versioning strategy

All services use URL versioning (`/v1/`). Phase 2 additions to existing services add new endpoints rather than versioning existing ones. Breaking changes get a new version prefix.

### Common response headers

Every response from every service must include:
- `X-Trace-Id: {correlationId}` — for distributed trace correlation
- `X-Request-Id: {requestId}` — unique per request
- `X-Service-Version: 1.0.0` — for debugging in Aspire Dashboard

---

*Document: LMS Phase 1 OpenAPI Specifications · Version 1.0 · April 2026*  
*Next: Phase 2 OpenAPI Specifications (Gamification · Live Sessions · Forums · Payment · Analytics · Adaptive Assessment · AI Tutor · Plagiarism · Recommendation)*
