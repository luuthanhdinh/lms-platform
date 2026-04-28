import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type {
  Course,
  CourseSummary,
  PagedResult,
  ListParams,
  CreateCourseRequest,
  SectionWithLessons,
  UpdateCourseRequest,
} from '@/types'

export const courseKeys = {
  all: () => ['courses'] as const,
  list: (p: ListParams) => ['courses', 'list', p] as const,
  detail: (id: string) => ['courses', 'detail', id] as const,
  syllabus: (id: string) => ['courses', 'syllabus', id] as const,
}

export function useCourses(params: ListParams = {}) {
  return useQuery({
    queryKey: courseKeys.list(params),
    queryFn: ({ signal }) =>
      apiClient
        .get<PagedResult<CourseSummary>>('/api/courses', { params, signal })
        .then(r => r.data),
    staleTime: 5 * 60_000,
  })
}

export function useCourse(courseId: string) {
  return useQuery({
    queryKey: courseKeys.detail(courseId),
    queryFn: ({ signal }) =>
      apiClient.get<Course>(`/api/courses/${courseId}`, { signal }).then(r => r.data),
    enabled: !!courseId,
  })
}

export function useCourseSyllabus(courseId: string) {
  return useQuery({
    queryKey: courseKeys.syllabus(courseId),
    queryFn: ({ signal }) =>
      apiClient
        .get<SectionWithLessons[]>(`/api/courses/${courseId}/syllabus`, { signal })
        .then(r => r.data),
    enabled: !!courseId,
  })
}

export function useCreateCourse() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateCourseRequest) =>
      apiClient.post<Course>('/api/courses', data).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.all() })
    },
  })
}

export function useUpdateCourse() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCourseRequest }) =>
      apiClient.put<Course>(`/api/courses/${id}`, data).then(r => r.data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: courseKeys.detail(id) })
      qc.invalidateQueries({ queryKey: courseKeys.all() })
    },
  })
}

export function usePublishCourse() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (courseId: string) =>
      apiClient.post<Course>(`/api/courses/${courseId}/publish`).then(r => r.data),
    onSuccess: (_, courseId) => {
      qc.invalidateQueries({ queryKey: courseKeys.detail(courseId) })
      qc.invalidateQueries({ queryKey: courseKeys.all() })
    },
  })
}
