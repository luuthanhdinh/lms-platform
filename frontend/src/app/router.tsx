import {
  createRouter,
  createRoute,
  createRootRoute,
  Outlet,
} from '@tanstack/react-router'
import { RootLayout } from './RootLayout'
import { LandingPage } from '@/features/landing/pages/LandingPage'
import { NotFoundPage } from '@/features/notfound/pages/NotFoundPage'
import { CataloguePage } from '@/features/courses/pages/CataloguePage'
import { CourseDetailPage } from '@/features/courses/pages/CourseDetailPage'
import { CourseEditorPage } from '@/features/courses/pages/CourseEditorPage'
import { InstructorCoursesPage } from '@/features/courses/pages/InstructorCoursesPage'
import { DashboardPage } from '@/features/dashboard/pages/DashboardPage'
import { LessonPage } from '@/features/content/pages/LessonPage'
import { ContentUploadPage } from '@/features/content/pages/ContentUploadPage'
import { QuizPage } from '@/features/assessment/pages/QuizPage'
import { MyCertificatesPage } from '@/features/certificate/pages/MyCertificatesPage'
import { VerifyPage } from '@/features/certificate/pages/VerifyPage'
import { ProtectedRoute } from '@/features/auth/components/ProtectedRoute'

const rootRoute = createRootRoute({
  component: () => (
    <RootLayout>
      <Outlet />
    </RootLayout>
  ),
  notFoundComponent: NotFoundPage,
})

const landingRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: LandingPage,
})

const catalogueRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses',
  component: CataloguePage,
})

const courseDetailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses/$courseId',
  component: function CourseDetailRoute() {
    const { courseId } = courseDetailRoute.useParams()
    return <CourseDetailPage courseId={courseId} />
  },
})

const dashboardRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/dashboard',
  component: () => (
    <ProtectedRoute>
      <DashboardPage />
    </ProtectedRoute>
  ),
})

const lessonRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses/$courseId/lessons/$lessonId',
  component: function LessonRoute() {
    const { courseId, lessonId } = lessonRoute.useParams()
    return (
      <ProtectedRoute>
        <LessonPage courseId={courseId} lessonId={lessonId} />
      </ProtectedRoute>
    )
  },
})

const quizRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses/$courseId/assessments/$assessmentId',
  component: function QuizRoute() {
    const { courseId, assessmentId } = quizRoute.useParams()
    return (
      <ProtectedRoute>
        <QuizPage courseId={courseId} assessmentId={assessmentId} />
      </ProtectedRoute>
    )
  },
})

const certificatesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/certificates',
  component: () => (
    <ProtectedRoute>
      <MyCertificatesPage />
    </ProtectedRoute>
  ),
})

const verifyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/verify/$code',
  component: function VerifyRoute() {
    const { code } = verifyRoute.useParams()
    return <VerifyPage code={code} />
  },
})

const instructorCoursesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/instructor/courses',
  component: () => (
    <ProtectedRoute roles={['instructor', 'admin']}>
      <InstructorCoursesPage />
    </ProtectedRoute>
  ),
})

const courseEditorRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/instructor/courses/$courseId/edit',
  component: function CourseEditorRoute() {
    const { courseId } = courseEditorRoute.useParams()
    return (
      <ProtectedRoute roles={['instructor', 'admin']}>
        <CourseEditorPage courseId={courseId} />
      </ProtectedRoute>
    )
  },
})

const contentUploadRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/instructor/courses/$courseId/content',
  component: function ContentUploadRoute() {
    const { courseId } = contentUploadRoute.useParams()
    return (
      <ProtectedRoute roles={['instructor', 'admin']}>
        <ContentUploadPage courseId={courseId} />
      </ProtectedRoute>
    )
  },
})

const routeTree = rootRoute.addChildren([
  landingRoute,
  catalogueRoute,
  courseDetailRoute,
  dashboardRoute,
  lessonRoute,
  quizRoute,
  certificatesRoute,
  verifyRoute,
  instructorCoursesRoute,
  courseEditorRoute,
  contentUploadRoute,
])

export const router = createRouter({ routeTree })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
