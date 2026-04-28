import { Link } from '@tanstack/react-router'
import { useCourses } from '../api/use-courses'
import { ErrorBanner } from '@/components/feedback/ErrorBanner'
import { Skeleton } from '@/components/feedback/Skeleton'

export function CataloguePage() {
  const { data, isLoading, isError } = useCourses({ page: 1, pageSize: 24 })

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-48 w-full" />
        ))}
      </div>
    )
  }

  if (isError) {
    return <ErrorBanner />
  }

  return (
    <div>
      <h1 className="mb-8 text-3xl font-bold text-foreground">Course Catalogue</h1>
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
        {data?.data.map(course => (
          <Link
            key={course.id}
            to="/courses/$courseId"
            params={{ courseId: course.id }}
            className="block rounded-lg border border-border bg-card p-4 hover:shadow-md"
          >
            <div className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {course.category}
            </div>
            <h2 className="mb-1 font-semibold text-card-foreground">{course.title}</h2>
            <p className="line-clamp-2 text-sm text-muted-foreground">{course.description}</p>
            <div className="mt-3 text-xs text-muted-foreground">
              {course.lessonCount} lessons · {course.enrollmentCount} enrolled
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
