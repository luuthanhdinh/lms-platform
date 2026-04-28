import { useCourses } from '../api/use-courses'
import { ErrorBanner } from '@/components/feedback/ErrorBanner'
import { Skeleton } from '@/components/feedback/Skeleton'

export function InstructorCoursesPage() {
  const { data, isLoading, isError } = useCourses()

  if (isLoading) return <Skeleton className="h-64 w-full" />
  if (isError) return <ErrorBanner />

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-3xl font-bold text-foreground">My Courses</h1>
        <a
          href="/instructor/courses/new/edit"
          className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          New Course
        </a>
      </div>
      <div className="space-y-4">
        {(data?.items ?? []).map(course => (
          <div key={course.id} className="flex items-center justify-between rounded-lg border border-border p-4">
            <div>
              <h2 className="font-medium text-foreground">{course.title}</h2>
              <p className="text-sm text-muted-foreground">{course.status}</p>
            </div>
            <a
              href={`/instructor/courses/${course.id}/edit`}
              className="text-sm text-primary hover:underline"
            >
              Edit
            </a>
          </div>
        ))}
      </div>
    </div>
  )
}
