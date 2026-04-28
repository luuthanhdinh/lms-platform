import { useCourse } from '../api/use-courses'
import { Skeleton } from '@/components/feedback/Skeleton'

interface Props {
  courseId: string
}

export function CourseEditorPage({ courseId }: Props) {
  const { data: course, isLoading } = useCourse(courseId)

  if (isLoading) return <Skeleton className="h-96 w-full" />

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-6 text-2xl font-bold text-foreground">
        {course ? `Editing: ${course.title}` : 'New Course'}
      </h1>
      <p className="text-muted-foreground">Course editor — coming in T5.</p>
    </div>
  )
}
