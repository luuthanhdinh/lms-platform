// ── Pagination ────────────────────────────────────────────────────────────────
export interface PaginatedMeta {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  total: number
}

// ── Courses ───────────────────────────────────────────────────────────────────
export interface CourseSummary {
  id: string
  title: string
  description: string
  thumbnailUrl: string | null
  category: string
  tags: string[]
  difficulty: 'beginner' | 'intermediate' | 'advanced'
  language: string
  instructorName: string
  enrollmentCount: number
  lessonCount: number
  totalDurationSeconds: number
  status: 'draft' | 'published'
  publishedAt: string | null
}

export interface SectionWithLessons {
  id: string
  title: string
  order: number
  lessons: LessonSummary[]
}

export interface LessonSummary {
  id: string
  title: string
  durationSeconds: number | null
  isFreePreview: boolean
  isOptional: boolean
  contentType: 'video' | 'pdf' | 'scorm' | 'h5p'
  order: number
}

export interface Course extends CourseSummary {
  tenantId: string
  instructorId: string
  version: number
  sections: SectionWithLessons[]
  prerequisites: { courseId: string; courseTitle: string }[]
}

// ── Enrollment ────────────────────────────────────────────────────────────────
export interface Enrollment {
  id: string
  userId: string
  courseId: string
  status: 'pending' | 'active' | 'completed' | 'suspended'
  enrolledAt: string
  completedAt: string | null
  expiresAt: string | null
}

// ── Progress ──────────────────────────────────────────────────────────────────
export interface CourseProgress {
  courseId: string
  completionPercent: number
  lessonsCompleted: number
  totalRequiredLessons: number
  lastAccessedAt: string | null
  completedAt: string | null
}

// ── Request types ─────────────────────────────────────────────────────────────
export interface CreateCourseRequest {
  title: string
  description: string
  category: string
  tags: string[]
  difficulty: 'beginner' | 'intermediate' | 'advanced'
  language: string
}

export interface UpdateCourseRequest extends Partial<CreateCourseRequest> {}

export interface ListParams {
  page?: number
  pageSize?: number
  category?: string
  q?: string
}

// ── Error response ────────────────────────────────────────────────────────────
export interface ErrorResponse {
  code: string
  message: string
  details?: { field: string; message: string }[]
  traceId?: string
}

// ── Assessment ────────────────────────────────────────────────────────────────
export interface QuestionOptionDto {
  id: string
  text: string
}

export interface QuestionDto {
  id: string
  text: string
  type: 'single' | 'multi' | 'free_text'
  options?: QuestionOptionDto[]
}

export interface SessionStartedDto {
  sessionId: string
  assessmentId: string
  questions: QuestionDto[]
  expiresAt: string
}

export interface AnswerDto {
  questionId: string
  selectedOptionIds?: string[]
  freeText?: string
}

export interface AssessmentResultDto {
  sessionId: string
  score: number
  passed: boolean
  correctCount: number
  total: number
}

export interface AttemptSummaryDto {
  id: string
  score: number
  passed: boolean
  submittedAt: string
}

// ── Certificate ───────────────────────────────────────────────────────────────
export interface CertificateSummary {
  id: string
  courseId: string
  courseTitle: string
  issuedAt: string
  verificationCode: string
  pdfUrl: string
}

export interface CertificateDetail extends CertificateSummary {
  learnerName: string
  certificateNumber: string
}

export interface VerificationResult {
  isValid: boolean
  certificate?: CertificateDetail
}

// ── Content ───────────────────────────────────────────────────────────────────
export interface ContentItem {
  id: string
  lessonId: string
  contentType: 'video' | 'pdf' | 'scorm' | 'h5p'
  status: 'pending' | 'processing' | 'ready' | 'failed'
  createdAt: string
}
