import { useQuery, useMutation } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { SessionStartedDto, AnswerDto, AssessmentResultDto, AttemptSummaryDto } from '@/types'

export const assessmentKeys = {
  attempts: (assessmentId: string) => ['assessments', assessmentId, 'attempts', 'me'] as const,
}

export function useStartAssessmentSession() {
  return useMutation<SessionStartedDto, Error, string>({
    mutationFn: assessmentId =>
      apiClient.post<SessionStartedDto>(`/api/assessments/${assessmentId}/sessions`).then(r => r.data),
  })
}

export function useSubmitAssessment() {
  return useMutation<AssessmentResultDto, Error, { sessionId: string; answers: AnswerDto[] }>({
    mutationFn: ({ sessionId, answers }) =>
      apiClient
        .post<AssessmentResultDto>(`/api/assessments/sessions/${sessionId}/submit`, { answers })
        .then(r => r.data),
  })
}

export function useMyAttempts(assessmentId: string) {
  return useQuery({
    queryKey: assessmentKeys.attempts(assessmentId),
    queryFn: ({ signal }) =>
      apiClient
        .get<AttemptSummaryDto[]>(`/api/assessments/${assessmentId}/attempts/me`, { signal })
        .then(r => r.data),
    enabled: !!assessmentId,
  })
}
