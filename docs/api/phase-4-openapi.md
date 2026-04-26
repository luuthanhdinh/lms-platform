# LMS Platform — Phase 4 OpenAPI Specifications

**Phase:** 4 — Innovation
**OpenAPI version:** 3.1.0
**Services:** Virtual Lab · Certificate Service (OB3 extension) · Engagement · Translation

---

## Table of Contents

1. [Virtual Lab Service](#1-virtual-lab-service)
2. [Certificate Service — Phase 4 Extensions](#2-certificate-service--phase-4-extensions)
3. [Engagement Service](#3-engagement-service)
4. [Translation Service](#4-translation-service)

---

## 1. Virtual Lab Service

**Base URL:** `/api/labs` | **Lab proxy:** `/labs/proxy/{sessionId}/` | **Port:** 5401
**DB:** `lms_labs` (PostgreSQL) | **Infra:** Kubernetes Jobs API + YARP lab-gateway

```yaml
openapi: 3.1.0
info:
  title: LMS Virtual Lab Service
  version: 1.0.0
  description: >
    Provisions ephemeral Kubernetes lab environments (browser terminal or VS Code)
    on demand. Each session gets an isolated Namespace with ResourceQuota
    and NetworkPolicy. Validation scripts run inside the Pod via k8s exec API.

servers:
  - url: /api/labs

tags:
  - name: Definitions
    description: Lab exercise configuration (instructor)
  - name: Sessions
    description: Student lab session lifecycle
  - name: Admin
    description: Resource governance and platform monitoring

paths:

  /definitions:
    post:
      tags: [Definitions]
      summary: Create a lab definition (instructor)
      operationId: createLabDefinition
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateLabDefinitionRequest' }
      responses:
        '201':
          description: Lab definition created
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabDefinition' }
        '400': { $ref: '#/components/responses/BadRequest' }

  /definitions:
    get:
      tags: [Definitions]
      summary: List lab definitions for a course
      operationId: listLabDefinitions
      parameters:
        - { name: courseId, in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Lab definitions
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/LabDefinition' } }

  /definitions/{labId}:
    get:
      tags: [Definitions]
      summary: Get lab definition
      operationId: getLabDefinition
      parameters:
        - { $ref: '#/components/parameters/labId' }
      responses:
        '200':
          description: Lab definition
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabDefinition' }

    put:
      tags: [Definitions]
      summary: Update lab definition (instructor — only if no active sessions)
      operationId: updateLabDefinition
      parameters:
        - { $ref: '#/components/parameters/labId' }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UpdateLabDefinitionRequest' }
      responses:
        '200':
          description: Updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabDefinition' }
        '409': { description: Active sessions exist for this definition }

    delete:
      tags: [Definitions]
      summary: Soft-delete lab definition
      operationId: deleteLabDefinition
      parameters:
        - { $ref: '#/components/parameters/labId' }
      responses:
        '204': { description: Deleted }
        '409': { description: Active sessions exist }

  /definitions/{labId}/analytics:
    get:
      tags: [Definitions]
      summary: Lab analytics for instructor
      operationId: getLabAnalytics
      parameters:
        - { $ref: '#/components/parameters/labId' }
      responses:
        '200':
          description: Analytics
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabAnalytics' }

  /sessions:
    post:
      tags: [Sessions]
      summary: Start a lab session (requires enrollment)
      operationId: startSession
      description: >
        Validates enrollment. Fails if active session already exists for this user+lab.
        Provisions Kubernetes Namespace, ResourceQuota, NetworkPolicy, and Job.
        Returns status=Provisioning immediately; poll GET /sessions/{id} for Ready.
        Target time to Ready: < 60 seconds.
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [labDefinitionId]
              properties:
                labDefinitionId: { $ref: '#/components/schemas/Uuid' }
      responses:
        '201':
          description: Session provisioning started
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabSession' }
        '403': { description: Not enrolled }
        '409': { description: Active session already exists for this lab }

  /sessions/active:
    get:
      tags: [Sessions]
      summary: Get active session for a specific lab (for resume)
      operationId: getActiveSession
      parameters:
        - { name: labDefinitionId, in: query, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Active session (for reconnect)
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabSession' }
        '404': { description: No active session }

  /sessions/{sessionId}:
    get:
      tags: [Sessions]
      summary: Get session status and access URL
      operationId: getSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Session detail
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LabSession' }

  /sessions/{sessionId}/validate:
    post:
      tags: [Sessions]
      summary: Trigger validation script inside pod
      operationId: validateSession
      description: >
        Executes validationScript inside the running Pod via Kubernetes exec API.
        Streams stdout/stderr output. Returns pass/fail + output.
        On pass: publishes LabCompleted event (marks lesson complete + awards XP).
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Validation result
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ValidationResult' }
        '404': { description: Session not found or terminated }
        '408': { description: Validation timed out }

  /sessions/{sessionId}/extend:
    post:
      tags: [Sessions]
      summary: Extend session TTL by 30 minutes (max 1 extension per session)
      operationId: extendSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Extended
          content:
            application/json:
              schema:
                type: object
                properties:
                  newExpiresAt:    { type: string, format: date-time }
                  remainingMinutes:{ type: integer }
        '409': { description: Extension already used for this session }

  /sessions/{sessionId}/terminate:
    post:
      tags: [Sessions]
      summary: Terminate session explicitly (student or system)
      operationId: terminateSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      responses:
        '200':
          description: Termination triggered (async teardown)
          content:
            application/json:
              schema:
                type: object
                properties:
                  status:       { type: string, enum: [terminating] }
                  terminatedAt: { type: string, format: date-time }

  /sessions/{sessionId}/hints/{hintOrder}:
    get:
      tags: [Sessions]
      summary: Reveal a hint (incremental; each call reveals next hint)
      operationId: revealHint
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
        - { name: hintOrder, in: path, required: true, schema: { type: integer, minimum: 1 } }
      responses:
        '200':
          description: Hint content
          content:
            application/json:
              schema:
                type: object
                properties:
                  hintOrder: { type: integer }
                  content:   { type: string }
                  total:     { type: integer }
        '404': { description: No hint at this order index }

  /admin/resources:
    get:
      tags: [Admin]
      summary: Platform-wide lab resource usage (platform admin)
      operationId: getResourceUsage
      responses:
        '200':
          description: Resource usage
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ResourceUsage' }

  /admin/sessions:
    get:
      tags: [Admin]
      summary: List all active sessions with resource usage (platform admin)
      operationId: listAllSessions
      parameters:
        - { name: tenantId, in: query, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: page,     in: query, schema: { type: integer, default: 1 } }
        - { name: pageSize, in: query, schema: { type: integer, default: 50 } }
      responses:
        '200':
          description: Active sessions
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/AdminSessionView' } }
                  meta: { $ref: '#/components/schemas/PaginatedMeta' }

  /admin/sessions/{sessionId}/terminate:
    post:
      tags: [Admin]
      summary: Force-terminate any session (platform admin)
      operationId: adminTerminateSession
      parameters:
        - { $ref: '#/components/parameters/sessionId' }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [reason]
              properties:
                reason: { type: string }
      responses:
        '200': { description: Termination triggered }

components:
  parameters:
    labId:
      name: labId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }
    sessionId:
      name: sessionId
      in: path
      required: true
      schema: { $ref: '#/components/schemas/Uuid' }

  schemas:
    CreateLabDefinitionRequest:
      type: object
      required: [lessonId, title, dockerImage, interfaceType, ttlMinutes, validationScriptUrl]
      properties:
        lessonId:            { $ref: '#/components/schemas/Uuid' }
        title:               { type: string, maxLength: 200 }
        description:         { type: string, maxLength: 1000 }
        dockerImage:
          type: string
          example: lmsregistry.azurecr.io/labs/python3:3.11
          description: Must be from approved registry whitelist
        startupScript:       { type: string, nullable: true, description: Bash script injected as ConfigMap }
        interfaceType:
          type: string
          enum: [Terminal, VSCode, Both]
        ttlMinutes:
          type: integer
          minimum: 15
          maximum: 240
          default: 60
        validationScriptUrl: { type: string, format: uri, description: S3 URL to validation.sh or validation.py }
        validationTimeout:   { type: integer, default: 30, minimum: 5, maximum: 300 }
        resourceLimits:
          type: object
          properties:
            cpu:    { type: string, example: 500m }
            memory: { type: string, example: 512Mi }
        hints:
          type: array
          maxItems: 10
          items:
            type: object
            required: [order, content]
            properties:
              order:   { type: integer, minimum: 1 }
              content: { type: string, maxLength: 2000 }

    UpdateLabDefinitionRequest:
      type: object
      properties:
        title:               { type: string, maxLength: 200 }
        description:         { type: string }
        ttlMinutes:          { type: integer, minimum: 15, maximum: 240 }
        validationScriptUrl: { type: string, format: uri }
        hints:               { type: array, items: { type: object } }

    LabDefinition:
      type: object
      properties:
        id:               { $ref: '#/components/schemas/Uuid' }
        lessonId:         { $ref: '#/components/schemas/Uuid' }
        courseId:         { $ref: '#/components/schemas/Uuid' }
        tenantId:         { $ref: '#/components/schemas/Uuid' }
        instructorId:     { $ref: '#/components/schemas/Uuid' }
        title:            { type: string }
        description:      { type: string }
        dockerImage:      { type: string }
        interfaceType:    { type: string, enum: [Terminal, VSCode, Both] }
        ttlMinutes:       { type: integer }
        validationTimeout:{ type: integer }
        resourceLimits:   { type: object }
        hintCount:        { type: integer }
        isActive:         { type: boolean }
        createdAt:        { type: string, format: date-time }

    LabSession:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        labDefinitionId: { $ref: '#/components/schemas/Uuid' }
        userId:          { $ref: '#/components/schemas/Uuid' }
        tenantId:        { $ref: '#/components/schemas/Uuid' }
        status:
          type: string
          enum: [Provisioning, Ready, Validated, Terminating, Terminated, Failed]
        accessUrl:
          type: string
          format: uri
          nullable: true
          description: Proxy URL e.g. /labs/proxy/{sessionId}/ (available when status=Ready)
        terminalUrl:
          type: string
          format: uri
          nullable: true
          description: /labs/proxy/{sessionId}/terminal — available when interfaceType includes Terminal
        vscodeUrl:
          type: string
          format: uri
          nullable: true
          description: /labs/proxy/{sessionId}/vscode — available when interfaceType includes VSCode
        remainingMinutes:{ type: integer, nullable: true }
        expiresAt:       { type: string, format: date-time, nullable: true }
        extensionUsed:   { type: boolean }
        validationAttempts:{ type: integer }
        validationPassed:  { type: boolean }
        hintsRevealed:     { type: integer }
        startedAt:         { type: string, format: date-time }

    ValidationResult:
      type: object
      properties:
        sessionId:    { $ref: '#/components/schemas/Uuid' }
        passed:       { type: boolean }
        score:        { type: number, format: float, nullable: true }
        output:       { type: string, description: stdout + stderr from validation script }
        durationMs:   { type: integer }
        attemptNumber:{ type: integer }

    LabAnalytics:
      type: object
      properties:
        labDefinitionId:       { $ref: '#/components/schemas/Uuid' }
        totalSessions:         { type: integer }
        completionRate:        { type: number, format: float }
        avgTimeToCompleteSeconds: { type: integer }
        avgValidationAttempts: { type: number, format: float }
        avgHintsRevealed:      { type: number, format: float }

    ResourceUsage:
      type: object
      properties:
        activeSessions:  { type: integer }
        totalCpuUsage:   { type: string, description: Kubernetes quantity, e.g. 4.2 }
        totalMemoryUsage:{ type: string, description: Kubernetes quantity, e.g. 2Gi }
        nodeCount:       { type: integer }
        costEstimateHourly: { type: number, format: float, description: USD/hour }
        sessionsPerCourse:
          type: array
          items:
            type: object
            properties:
              courseId:     { $ref: '#/components/schemas/Uuid' }
              courseTitle:  { type: string }
              activeSessions: { type: integer }

    AdminSessionView:
      type: object
      properties:
        sessionId:      { $ref: '#/components/schemas/Uuid' }
        tenantId:       { $ref: '#/components/schemas/Uuid' }
        userId:         { $ref: '#/components/schemas/Uuid' }
        labTitle:       { type: string }
        status:         { type: string }
        cpuUsage:       { type: string }
        memoryUsage:    { type: string }
        startedAt:      { type: string, format: date-time }
        expiresAt:      { type: string, format: date-time }
```

---

## 2. Certificate Service — Phase 4 Extensions

Extends the Phase 1 Certificate Service. Additional endpoints on port 5107.
Public JWKS endpoint: `GET /.well-known/jwks.json`
Revocation list: `GET /credentials/status/{listId}`

```yaml
openapi: 3.1.0
info:
  title: LMS Certificate Service — Open Badges 3.0 Extensions
  version: 2.0.0
  description: >
    Phase 4 upgrades certificate issuance to Open Badges 3.0 (W3C Verifiable Credentials).
    Credentials signed with Ed25519 stored in Azure Key Vault.
    Public verification page at /credentials/{credentialId}.
    Optional Polygon blockchain anchoring (per-tenant opt-in).
    Status List 2021 revocation standard.

servers:
  - url: /

tags:
  - name: Credentials
  - name: Verification
    description: Public — no auth
  - name: Revocation
  - name: Blockchain
  - name: JWKS

paths:

  /api/certificates/wallet:
    get:
      tags: [Credentials]
      summary: Get full credential wallet for current user
      operationId: getWallet
      responses:
        '200':
          description: Wallet items
          content:
            application/json:
              schema:
                type: object
                properties:
                  data: { type: array, items: { $ref: '#/components/schemas/WalletItem' } }

  /api/certificates/{certificateId}/share:
    get:
      tags: [Credentials]
      summary: Get shareable card and LinkedIn share URL
      operationId: getShareCard
      parameters:
        - { name: certificateId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Share URLs
          content:
            application/json:
              schema:
                type: object
                properties:
                  shareCardUrl:    { type: string, format: uri, description: 1200x630 OG image }
                  linkedInShareUrl:{ type: string, format: uri }
                  credentialJwt:   { type: string, description: OB3 signed JWT for wallet import }

  /api/certificates/{certificateId}/revoke:
    post:
      tags: [Revocation]
      summary: Revoke a credential (admin only)
      operationId: revokeCredential
      parameters:
        - { name: certificateId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [reason]
              properties:
                reason: { type: string, maxLength: 500 }
      responses:
        '200':
          description: Revoked (verification page updated; student notified)
          content:
            application/json:
              schema:
                type: object
                properties:
                  credentialId: { $ref: '#/components/schemas/Uuid' }
                  revokedAt:    { type: string, format: date-time }

  /credentials/{credentialId}:
    get:
      tags: [Verification]
      summary: Verify a credential — public, no auth
      operationId: verifyCredential
      security: []
      description: >
        Returns HTML for browsers (Accept: text/html).
        Returns full OB3 JSON-LD for API consumers (Accept: application/json).
        Checks: JWT signature, not expired, not revoked (Status List 2021).
      parameters:
        - { name: credentialId, in: path, required: true, schema: { type: string, format: uuid } }
      responses:
        '200':
          description: Verification page or JSON-LD credential
          content:
            application/json:
              schema: { $ref: '#/components/schemas/VerificationResult' }
            text/html:
              schema: { type: string }
        '404': { description: Credential not found }

  /credentials/status/{listId}:
    get:
      tags: [Revocation]
      summary: Status List 2021 revocation bitstring — public
      operationId: getStatusList
      security: []
      description: >
        W3C Status List 2021 bitstring encoded as Base64URL.
        Consumed by credential verifiers to check revocation without calling back to LMS.
      parameters:
        - { name: listId, in: path, required: true, schema: { type: string } }
      responses:
        '200':
          description: Status List 2021 Verifiable Credential
          content:
            application/json:
              schema:
                type: object
                properties:
                  '@context':  { type: array, items: { type: string } }
                  id:          { type: string, format: uri }
                  type:        { type: array, items: { type: string } }
                  statusPurpose: { type: string, enum: [revocation] }
                  encodedList: { type: string, description: Base64URL-encoded bitstring }

  /.well-known/jwks.json:
    get:
      tags: [JWKS]
      summary: Platform Ed25519 JWK set for credential verification — public
      operationId: getJwks
      security: []
      description: >
        JWK Set containing the platform Ed25519 public key used to sign all OB3 JWTs.
        Use this to verify credential signatures independently.
      responses:
        '200':
          description: JWK Set
          content:
            application/json:
              schema:
                type: object
                properties:
                  keys:
                    type: array
                    items:
                      type: object
                      properties:
                        kty:  { type: string, enum: [OKP] }
                        crv:  { type: string, enum: [Ed25519] }
                        use:  { type: string, enum: [sig] }
                        kid:  { type: string }
                        x:    { type: string, description: Base64URL-encoded public key }

  /api/tenant/{tenantId}/blockchain:
    put:
      tags: [Blockchain]
      summary: Enable/configure blockchain anchoring (platform admin)
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

  /api/certificates/anchoring/status:
    get:
      tags: [Blockchain]
      summary: Anchoring queue health (platform admin)
      operationId: getAnchoringStatus
      responses:
        '200':
          description: Anchoring health
          content:
            application/json:
              schema:
                type: object
                properties:
                  pendingCount:  { type: integer }
                  walletBalance: { type: string, description: MATIC balance }
                  network:       { type: string }
                  isHealthy:     { type: boolean }

components:
  schemas:
    WalletItem:
      type: object
      properties:
        id:              { $ref: '#/components/schemas/Uuid' }
        courseId:        { $ref: '#/components/schemas/Uuid' }
        courseTitle:     { type: string }
        credentialId:    { type: string, format: uuid }
        credentialJwt:   { type: string, description: Signed OB3 JWT }
        verifyUrl:       { type: string, format: uri }
        linkedInShareUrl:{ type: string, format: uri }
        pdfDownloadUrl:  { type: string, format: uri }
        issuedAt:        { type: string, format: date-time }
        revokedAt:       { type: string, format: date-time, nullable: true }
        anchorStatus:    { type: string, enum: [not_anchored, pending, confirmed, failed] }
        blockchainAnchor:
          type: object
          nullable: true
          properties:
            txHash:      { type: string }
            blockNumber: { type: integer }
            network:     { type: string }
            explorerUrl: { type: string, format: uri }
            anchoredAt:  { type: string, format: date-time }

    VerificationResult:
      type: object
      properties:
        valid:           { type: boolean }
        studentName:     { type: string, nullable: true }
        courseTitle:     { type: string, nullable: true }
        issuerName:      { type: string, nullable: true }
        issuedAt:        { type: string, format: date-time, nullable: true }
        completedAt:     { type: string, format: date-time, nullable: true }
        revokedAt:       { type: string, format: date-time, nullable: true }
        revocationReason:{ type: string, nullable: true }
        badgeImageUrl:   { type: string, format: uri, nullable: true }
        blockchainAnchor:
          type: object
          nullable: true
          properties:
            txHash:      { type: string }
            network:     { type: string }
            explorerUrl: { type: string, format: uri }
            anchoredAt:  { type: string, format: date-time }
        credentialJsonLd:
          type: object
          nullable: true
          description: Full OB3 JSON-LD (included for application/json requests)
```

---

## 3. Engagement Service

**Base URL:** `/api/engagement` | **Port:** 5402 | **DB:** PostgreSQL + ClickHouse

```yaml
openapi: 3.1.0
info:
  title: LMS Engagement Service
  version: 1.0.0
  description: >
    Collects opt-in behavioural engagement signals from the video player
    and quiz renderer. Scores sessions for frustration and boredom.
    Triggers adaptive responses. Provides instructor weekly digests.
    All collection requires explicit user consent.

servers:
  - url: /api/engagement

tags:
  - name: Consent
  - name: Signals
  - name: Insights
  - name: Admin

paths:

  /consent:
    post:
      tags: [Consent]
      summary: Record engagement tracking consent decision
      operationId: recordConsent
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [consented]
              properties:
                consented: { type: boolean }
      responses:
        '200':
          description: Consent recorded
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ConsentStatus' }

  /consent/me:
    get:
      tags: [Consent]
      summary: Get current consent status for current user
      operationId: getMyConsent
      responses:
        '200':
          description: Consent status
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ConsentStatus' }

  /signals:
    post:
      tags: [Signals]
      summary: Submit batched engagement signals from client
      operationId: submitSignals
      description: >
        Requires prior consent (consented=true). Returns 403 if not consented.
        Signals batched by client every 30 seconds during video/quiz session.
        Triggers async scoring after receipt.
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [signals]
              properties:
                signals:
                  type: array
                  maxItems: 100
                  items: { $ref: '#/components/schemas/EngagementSignal' }
      responses:
        '204': { description: Signals recorded }
        '403': { description: Consent not given }

  /signals/me:
    delete:
      tags: [Signals]
      summary: Delete all stored signals for current user (GDPR right to erasure)
      operationId: deleteMySignals
      responses:
        '204': { description: All signals deleted }

  /sessions/{sessionId}/score:
    get:
      tags: [Signals]
      summary: Get current session frustration/boredom scores (internal)
      operationId: getSessionScore
      description: Internal endpoint used by frontend to check if adaptive response has been triggered.
      parameters:
        - { name: sessionId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Session scores
          content:
            application/json:
              schema: { $ref: '#/components/schemas/SessionScore' }

  /instructor/courses/{courseId}/insights:
    get:
      tags: [Insights]
      summary: Aggregated engagement insights per lesson (instructor)
      operationId: getCourseInsights
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: weekOf, in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Course insights
          content:
            application/json:
              schema:
                type: object
                properties:
                  courseId: { $ref: '#/components/schemas/Uuid' }
                  weekOf:   { type: string, format: date }
                  lessons:
                    type: array
                    items: { $ref: '#/components/schemas/LessonInsight' }

  /instructor/courses/{courseId}/digest:
    get:
      tags: [Insights]
      summary: Weekly instructor digest (same data as scheduled email)
      operationId: getWeeklyDigest
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: weekOf, in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Digest
          content:
            application/json:
              schema: { $ref: '#/components/schemas/WeeklyDigest' }

  /admin/config:
    put:
      tags: [Admin]
      summary: Update scoring weights and thresholds (platform admin)
      operationId: updateScoringConfig
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/ScoringConfig' }
      responses:
        '200':
          description: Config updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ScoringConfig' }

components:
  schemas:
    ConsentStatus:
      type: object
      properties:
        userId:      { $ref: '#/components/schemas/Uuid' }
        consented:   { type: boolean }
        consentedAt: { type: string, format: date-time, nullable: true }
        revokedAt:   { type: string, format: date-time, nullable: true }

    EngagementSignal:
      type: object
      required: [contentId, sessionId, signalType, value, clientTimestamp]
      properties:
        contentId:
          $ref: '#/components/schemas/Uuid'
          description: Lesson or content item ID
        sessionId:
          type: string
          format: uuid
          description: Client-generated session UUID (new per page load)
        signalType:
          type: string
          enum:
            - replay
            - pause_long
            - seek_forward
            - tab_hidden
            - idle
            - playback_rate
            - answer_change
            - time_on_question
            - rapid_submit
        value:
          type: number
          format: float
          description: >
            Signal-specific value:
            replay = seconds rewound,
            pause_long = pause duration seconds,
            seek_forward = seconds skipped,
            tab_hidden = 1,
            idle = idle duration seconds,
            playback_rate = new rate (e.g. 1.5),
            answer_change = change count,
            time_on_question = seconds,
            rapid_submit = ratio (actual/allowed time)
        clientTimestamp: { type: string, format: date-time }

    SessionScore:
      type: object
      properties:
        sessionId:        { type: string, format: uuid }
        frustrationScore: { type: number, format: float, minimum: 0, maximum: 1 }
        boredomScore:     { type: number, format: float, minimum: 0, maximum: 1 }
        frustrationTriggered: { type: boolean }
        boredomTriggered:     { type: boolean }
        lastScoredAt:         { type: string, format: date-time }

    LessonInsight:
      type: object
      properties:
        lessonId:          { $ref: '#/components/schemas/Uuid' }
        lessonTitle:       { type: string }
        avgFrustrationScore:{ type: number, format: float }
        avgBoredomScore:    { type: number, format: float }
        frustrationSpikes:
          type: array
          items:
            type: object
            properties:
              positionSeconds: { type: integer }
              count:           { type: integer }
        topReplaySegments:
          type: array
          items:
            type: object
            properties:
              startSeconds: { type: integer }
              replayCount:  { type: integer }

    WeeklyDigest:
      type: object
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid' }
        courseTitle: { type: string }
        weekOf:      { type: string, format: date }
        topFrustrationPoints:
          type: array
          maxItems: 3
          items:
            type: object
            properties:
              lessonId:      { $ref: '#/components/schemas/Uuid' }
              lessonTitle:   { type: string }
              positionSeconds:{ type: integer }
              affectedPct:   { type: number, format: float }
              suggestedAction:{ type: string }
        topBoredomPoints:
          type: array
          maxItems: 3
          items:
            type: object
            properties:
              lessonId:      { $ref: '#/components/schemas/Uuid' }
              lessonTitle:   { type: string }
              affectedPct:   { type: number, format: float }
              suggestedAction:{ type: string }

    ScoringConfig:
      type: object
      properties:
        frustrationThreshold: { type: number, format: float, default: 0.6 }
        boredomThreshold:     { type: number, format: float, default: 0.6 }
        deduplicationWindowMinutes: { type: integer, default: 15 }
        weights:
          type: object
          description: Map of signalType to { frustration_weight, boredom_weight }
          additionalProperties:
            type: object
            properties:
              frustrationWeight: { type: number, format: float }
              boredomWeight:     { type: number, format: float }
```

---

## 4. Translation Service

**Base URL:** `/api/translations` | **Port:** 5403 | **DB:** PostgreSQL

```yaml
openapi: 3.1.0
info:
  title: LMS Translation Service
  version: 1.0.0
  description: >
    Translates course lesson content and video captions using LLM.
    Video translation: LLM VTT translation (preserves timestamps) or
    Whisper translate mode as fallback when source VTT unavailable.
    100% instructor approval required before publishing.

servers:
  - url: /api/translations

tags:
  - name: Jobs
  - name: Review
  - name: Published

paths:

  /courses/{courseId}:
    post:
      tags: [Jobs]
      summary: Start translation job for a course (instructor)
      operationId: startTranslation
      description: >
        Returns cost estimate before proceeding. Instructor must confirm.
        Each lesson translated in parallel (max 5 concurrent).
        Video captions translated via LLM VTT translation or Whisper fallback.
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/TranslationJobRequest' }
      responses:
        '202':
          description: Job started
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TranslationJob' }
        '400': { $ref: '#/components/responses/BadRequest' }
        '409': { description: Translation job already in progress for this language }

  /courses/{courseId}/estimate:
    post:
      tags: [Jobs]
      summary: Get cost estimate before starting job (instructor)
      operationId: estimateCost
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [targetLanguage]
              properties:
                targetLanguage:       { type: string }
                includeVideoTranslation:{ type: boolean, default: false }
      responses:
        '200':
          description: Cost estimate
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CostEstimate' }

  /courses/{courseId}/languages:
    get:
      tags: [Published]
      summary: Get published languages for a course (public)
      operationId: getCourseLanguages
      security: []
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Published locales
          content:
            application/json:
              schema:
                type: object
                properties:
                  sourceLanguage: { type: string }
                  locales:
                    type: array
                    items:
                      type: object
                      properties:
                        language:    { type: string }
                        publishedAt: { type: string, format: date-time }

  /jobs/{jobId}:
    get:
      tags: [Jobs]
      summary: Get job status and progress
      operationId: getJobStatus
      parameters:
        - { name: jobId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
      responses:
        '200':
          description: Job status
          content:
            application/json:
              schema: { $ref: '#/components/schemas/TranslationJob' }

  /courses/{courseId}/review:
    get:
      tags: [Review]
      summary: Get all lesson translations for instructor review
      operationId: getReviewList
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: language, in: query, required: true, schema: { type: string } }
        - { name: status, in: query, schema: { type: string, enum: [draft, approved] } }
      responses:
        '200':
          description: Review list
          content:
            application/json:
              schema:
                type: object
                properties:
                  language:         { type: string }
                  totalLessons:     { type: integer }
                  approvedLessons:  { type: integer }
                  lessons:
                    type: array
                    items: { $ref: '#/components/schemas/LessonTranslationReview' }

  /lessons/{lessonId}:
    put:
      tags: [Review]
      summary: Update lesson translation body (instructor edit)
      operationId: updateLessonTranslation
      parameters:
        - { name: lessonId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: language, in: query, required: true, schema: { type: string } }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [translatedBody]
              properties:
                translatedBody: { type: string, description: Full translated markdown content }
      responses:
        '200':
          description: Updated
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LessonTranslation' }

  /lessons/{lessonId}/approve:
    post:
      tags: [Review]
      summary: Approve lesson translation
      operationId: approveLessonTranslation
      parameters:
        - { name: lessonId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: language, in: query, required: true, schema: { type: string } }
      responses:
        '200':
          description: Approved
          content:
            application/json:
              schema: { $ref: '#/components/schemas/LessonTranslation' }

  /courses/{courseId}/publish:
    post:
      tags: [Published]
      summary: Publish all translations as a locale (requires 100% approval)
      operationId: publishLocale
      description: >
        Blocked server-side if any lessons are not yet approved.
        Creates CourseLocale record. Triggers MarketplaceService re-indexing.
        Publishes CourseLocalePublished event.
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: language, in: query, required: true, schema: { type: string } }
      responses:
        '200':
          description: Locale published
          content:
            application/json:
              schema: { $ref: '#/components/schemas/PublishedLocale' }
        '409':
          description: Not all lessons approved
          content:
            application/json:
              schema:
                allOf:
                  - { $ref: '#/components/schemas/ErrorResponse' }
                  - type: object
                    properties:
                      unapprovedCount: { type: integer }

  /courses/{courseId}/locale/{language}:
    delete:
      tags: [Published]
      summary: Unpublish a locale (instructor/admin)
      operationId: unpublishLocale
      parameters:
        - { name: courseId, in: path, required: true, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: language, in: path, required: true, schema: { type: string } }
      responses:
        '204': { description: Unpublished }

  /me/preference:
    put:
      tags: [Published]
      summary: Set preferred language for a course (student)
      operationId: setLocalePreference
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [courseId, language]
              properties:
                courseId: { $ref: '#/components/schemas/Uuid' }
                language: { type: string }
      responses:
        '200': { description: Preference saved }
        '409': { description: Language not published for this course }

  /admin/budget:
    get:
      tags: [Review]
      summary: Translation token budget usage (platform/org admin)
      operationId: getBudgetUsage
      parameters:
        - { name: tenantId, in: query, schema: { $ref: '#/components/schemas/Uuid' } }
        - { name: from,     in: query, schema: { type: string, format: date } }
        - { name: to,       in: query, schema: { type: string, format: date } }
      responses:
        '200':
          description: Budget usage
          content:
            application/json:
              schema:
                type: object
                properties:
                  tokensUsed:      { type: integer }
                  tokenBudget:     { type: integer }
                  costUsd:         { type: number, format: float }
                  jobCount:        { type: integer }

components:
  schemas:
    TranslationJobRequest:
      type: object
      required: [targetLanguage]
      properties:
        targetLanguage:
          type: string
          enum: [vi, ar, fr, de, ja, ko, pt, es, zh-Hans]
        includeVideoTranslation: { type: boolean, default: false }
        confirmed:
          type: boolean
          default: false
          description: Must be true after cost estimate shown; else returns estimate only

    TranslationJob:
      type: object
      properties:
        id:               { $ref: '#/components/schemas/Uuid' }
        courseId:         { $ref: '#/components/schemas/Uuid' }
        tenantId:         { $ref: '#/components/schemas/Uuid' }
        instructorId:     { $ref: '#/components/schemas/Uuid' }
        targetLanguage:   { type: string }
        includeVideo:     { type: boolean }
        status:           { type: string, enum: [queued, translating, review, published, failed] }
        lessonsTotal:     { type: integer }
        lessonsCompleted: { type: integer }
        lessonsApproved:  { type: integer }
        tokensUsed:       { type: integer, nullable: true }
        costUsd:          { type: number, format: float, nullable: true }
        createdAt:        { type: string, format: date-time }
        completedAt:      { type: string, format: date-time, nullable: true }
        error:            { type: string, nullable: true }

    CostEstimate:
      type: object
      properties:
        targetLanguage:        { type: string }
        estimatedInputTokens:  { type: integer }
        estimatedOutputTokens: { type: integer }
        estimatedCostUsd:      { type: number, format: float }
        lessonCount:           { type: integer }
        videoCaptionCount:     { type: integer }
        modelUsed:             { type: string }

    LessonTranslation:
      type: object
      properties:
        id:               { $ref: '#/components/schemas/Uuid' }
        lessonId:         { $ref: '#/components/schemas/Uuid' }
        courseId:         { $ref: '#/components/schemas/Uuid' }
        language:         { type: string }
        status:           { type: string, enum: [draft, approved] }
        instructorEdited: { type: boolean }
        translatedAt:     { type: string, format: date-time }
        approvedAt:       { type: string, format: date-time, nullable: true }

    LessonTranslationReview:
      allOf:
        - { $ref: '#/components/schemas/LessonTranslation' }
        - type: object
          properties:
            lessonTitle:        { type: string }
            originalBody:       { type: string, description: Source language markdown }
            translatedBody:     { type: string, description: Translated markdown }
            bleuScore:          { type: number, format: float, nullable: true }
            captionTranslated:  { type: boolean }

    PublishedLocale:
      type: object
      properties:
        courseId:    { $ref: '#/components/schemas/Uuid' }
        language:    { type: string }
        publishedAt: { type: string, format: date-time }
```

---

## Lab Proxy Architecture Note

The lab proxy is **not** an API endpoint in the conventional sense. It is a YARP route registered dynamically per session:

```yaml
# Dynamic YARP route (registered when session status → Ready)
Route:
  RouteId: lab-{sessionId}
  Match:
    Path: /labs/proxy/{sessionId}/{**remainder}
  ClusterId: lab-cluster-{sessionId}

Cluster:
  ClusterId: lab-cluster-{sessionId}
  Destinations:
    pod:
      Address: http://{podIp}:{port}/
  SessionAffinity:
    Enabled: true

# Authentication: YARP validates JWT cookie before proxying
# Headers forwarded: X-User-Id, X-Session-Id
# WebSocket upgrade: enabled (required for ttyd terminal)
```

Sub-paths routed by interface type:
- `/labs/proxy/{sessionId}/terminal` → `ttyd` sidecar (port 7681) — WebSocket
- `/labs/proxy/{sessionId}/vscode` → `code-server` sidecar (port 8080) — HTTP

---

## Phase 4 Service Port Summary

| Service | Port | Auth |
|---|---|---|
| VirtualLabService | 5401 | JWT; enrollment required for sessions |
| CertificateService (ext) | 5107 | JWT for wallet; public for verify + JWKS + status |
| EngagementService | 5402 | JWT; consent required for signals |
| TranslationService | 5403 | JWT; instructor for job creation |
| Lab Proxy Gateway | 443/8443 | JWT cookie; dynamic YARP routes |

---

*Document: LMS Phase 4 OpenAPI Specifications · Version 1.0 · April 2026*
*This completes the full four-phase OpenAPI specification set.*
*All specs use OpenAPI 3.1.0 — implement with Swashbuckle in each .NET Aspire service.*
