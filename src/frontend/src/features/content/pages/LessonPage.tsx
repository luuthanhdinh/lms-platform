import { useContentStream } from '../api/use-content'
import { Skeleton } from '@/components/feedback/Skeleton'
import { ErrorBanner } from '@/components/feedback/ErrorBanner'

interface Props {
  courseId: string
  lessonId: string
  contentItemId?: string
}

export function LessonPage({ lessonId, contentItemId = lessonId }: Props) {
  const { data: stream, isLoading, isError } = useContentStream(contentItemId)

  if (isLoading) return <Skeleton className="aspect-video w-full" />
  if (isError) return <ErrorBanner />

  return (
    <div className="mx-auto max-w-4xl">
      {stream && (
        <video
          src={stream.url}
          controls
          className="w-full rounded-lg"
        />
      )}
    </div>
  )
}
