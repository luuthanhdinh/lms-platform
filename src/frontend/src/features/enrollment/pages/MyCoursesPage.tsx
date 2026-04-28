import { useMyEnrollments } from '../api/use-enrollments'
import { Skeleton } from '@/components/feedback/Skeleton'
import { EmptyState } from '@/components/feedback/EmptyState'

export function MyCoursesPage() {
  const { data: enrollments, isLoading } = useMyEnrollments()

  if (isLoading) return <Skeleton className="h-64 w-full" />

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold text-foreground">My Courses</h1>
      {!enrollments?.length ? (
        <EmptyState title="No enrolled courses" description="Enroll in a course to get started." />
      ) : (
        <div className="space-y-4">
          {enrollments.map(e => (
            <div key={e.id} className="rounded-lg border border-border p-4">
              <p className="font-medium text-foreground">{e.courseId}</p>
              <p className="text-sm text-muted-foreground">Status: {e.status}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
