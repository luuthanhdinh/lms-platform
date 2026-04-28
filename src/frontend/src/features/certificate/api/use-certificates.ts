import { useQuery } from '@tanstack/react-query'
import { apiClient, publicApiClient } from '@/lib/api-client'
import type { CertificateSummary, CertificateDetail, VerificationResult } from '@/types'

export const certificateKeys = {
  mine: () => ['certificates', 'me'] as const,
  detail: (id: string) => ['certificates', id] as const,
  verify: (code: string) => ['certificates', 'verify', code] as const,
}

export function useMyCertificates() {
  return useQuery({
    queryKey: certificateKeys.mine(),
    queryFn: ({ signal }) =>
      apiClient.get<CertificateSummary[]>('/api/certificates/me', { signal }).then(r => r.data),
  })
}

export function useCertificate(id: string) {
  return useQuery({
    queryKey: certificateKeys.detail(id),
    queryFn: ({ signal }) =>
      apiClient.get<CertificateDetail>(`/api/certificates/${id}`, { signal }).then(r => r.data),
    enabled: !!id,
  })
}

// Uses publicApiClient — no Authorization header; supports unauthenticated verify links
export function useVerifyCertificate(code: string) {
  return useQuery({
    queryKey: certificateKeys.verify(code),
    queryFn: ({ signal }) =>
      publicApiClient.get<VerificationResult>(`/verify/${code}`, { signal }).then(r => r.data),
    enabled: !!code,
  })
}
