import { useMyEnrollments } from '@/features/enrollment/api/use-enrollments'
import { useMyProgress } from '@/features/progress/api/use-progress'
import { Skeleton } from '@/components/feedback/Skeleton'
import { EmptyState } from '@/components/feedback/EmptyState'

export function DashboardPage() {
  const { data: enrollments, isLoading: loadingEnrollments } = useMyEnrollments()
  const { data: progressList, isLoading: loadingProgress } = useMyProgress()

  if (loadingEnrollments || loadingProgress) {
    return <Skeleton className="h-64 w-full" />
  }

  const progressMap = new Map(progressList?.map(p => [p.courseId, p]) ?? [])

  return (
    <div>
      <h1 className="mb-8 text-3xl font-bold text-foreground">My Dashboard</h1>
      {!enrollments?.length ? (
        <EmptyState title="No courses yet" description="Browse the catalogue and enroll in a course." />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {enrollments.map(enrollment => {
            const progress = progressMap.get(enrollment.courseId)
            return (
              <div key={enrollment.id} className="rounded-lg border border-border p-4">
                <div className="mb-2 font-medium text-foreground">{enrollment.courseId}</div>
                {progress && (
                  <div>
                    <div className="mb-1 flex items-center justify-between text-sm">
                      <span className="text-muted-foreground">Progress</span>
                      <span className="font-medium">{Math.round(progress.completionPercent)}%</span>
                    </div>
                    <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full rounded-full bg-primary transition-all w-[var(--progress)]"
                        style={{ '--progress': `${progress.completionPercent}%` } as React.CSSProperties}
                      />
                    </div>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
