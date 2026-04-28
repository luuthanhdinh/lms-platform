import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { Enrollment, ErrorResponse } from '@/types'
import type { AxiosError } from 'axios'

export const enrollmentKeys = {
  all: () => ['enrollments'] as const,
  mine: () => ['enrollments', 'me'] as const,
}

export function useMyEnrollments() {
  return useQuery({
    queryKey: enrollmentKeys.mine(),
    queryFn: ({ signal }) =>
      apiClient.get<Enrollment[]>('/api/enrollments/me', { signal }).then(r => r.data),
  })
}

export function useEnroll() {
  const qc = useQueryClient()
  return useMutation<Enrollment, AxiosError<ErrorResponse>, { courseId: string }>({
    mutationFn: data => apiClient.post<Enrollment>('/api/enrollments', data).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: enrollmentKeys.mine() })
    },
  })
}

export function useCancelEnrollment() {
  const qc = useQueryClient()
  return useMutation<void, AxiosError<ErrorResponse>, string>({
    mutationFn: enrollmentId =>
      apiClient.delete(`/api/enrollments/${enrollmentId}`).then(() => undefined),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: enrollmentKeys.mine() })
    },
  })
}
