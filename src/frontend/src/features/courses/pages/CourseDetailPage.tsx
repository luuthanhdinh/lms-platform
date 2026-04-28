import { useCourse, useCourseSyllabus } from '../api/use-courses'
import { ErrorBanner } from '@/components/feedback/ErrorBanner'
import { Skeleton } from '@/components/feedback/Skeleton'

interface Props {
  courseId: string
}

export function CourseDetailPage({ courseId }: Props) {
  const { data: course, isLoading, isError } = useCourse(courseId)
  const { data: syllabus } = useCourseSyllabus(courseId)

  if (isLoading) return <Skeleton className="h-96 w-full" />
  if (isError) return <ErrorBanner />
  if (!course) return null

  return (
    <div className="mx-auto max-w-4xl">
      <h1 className="mb-4 text-4xl font-bold text-foreground">{course.title}</h1>
      <p className="mb-6 text-muted-foreground">{course.description}</p>
      <div className="mb-8 flex gap-4 text-sm text-muted-foreground">
        <span>{course.difficulty}</span>
        <span>{course.language}</span>
        <span>{course.lessonCount} lessons</span>
        <span>{course.enrollmentCount} enrolled</span>
      </div>
      {syllabus && (
        <div>
          <h2 className="mb-4 text-xl font-semibold">Syllabus</h2>
          {syllabus.map(section => (
            <div key={section.id} className="mb-4">
              <h3 className="mb-2 font-medium">{section.title}</h3>
              <ul className="space-y-1">
                {section.lessons.map(lesson => (
                  <li key={lesson.id} className="flex items-center gap-2 text-sm text-muted-foreground">
                    <span>{lesson.title}</span>
                    {lesson.isFreePreview && (
                      <span className="rounded bg-primary/10 px-1 py-0.5 text-xs text-primary">
                        Free
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
