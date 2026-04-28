import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { CourseProgress } from '@/types'
import { enrollmentKeys } from '@/features/enrollment/api/use-enrollments'

export const progressKeys = {
  all: () => ['progress'] as const,
  mine: () => ['progress', 'me'] as const,
  course: (courseId: string) => ['progress', 'courses', courseId] as const,
}

export function useMyProgress() {
  return useQuery({
    queryKey: progressKeys.mine(),
    queryFn: ({ signal }) =>
      apiClient.get<CourseProgress[]>('/api/progress/me', { signal }).then(r => r.data),
  })
}

export function useCourseProgress(courseId: string) {
  return useQuery({
    queryKey: progressKeys.course(courseId),
    queryFn: ({ signal }) =>
      apiClient
        .get<CourseProgress>(`/api/progress/courses/${courseId}`, { signal })
        .then(r => r.data),
    enabled: !!courseId,
  })
}

export function useCompleteLesson() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({
      lessonId,
      courseId,
      watchPercent,
    }: {
      lessonId: string
      courseId: string
      watchPercent: number
    }) =>
      apiClient
        .post(`/api/progress/lessons/${lessonId}/complete`, { courseId, watchPercent })
        .then(() => undefined),
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: progressKeys.course(courseId) })
      qc.invalidateQueries({ queryKey: progressKeys.mine() })
      qc.invalidateQueries({ queryKey: enrollmentKeys.mine() })
    },
  })
}
