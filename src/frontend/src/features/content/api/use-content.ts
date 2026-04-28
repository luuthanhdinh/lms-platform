import { useQuery, useMutation } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { ContentItem } from '@/types'

interface ContentStream {
  url: string
  expiresAt: string
  resumePositionSeconds: number
}

interface ReportProgressInput {
  contentItemId: string
  lessonId: string
  positionSeconds: number
  totalSeconds: number
}

interface UploadContentInput {
  file: File
  lessonId: string
  contentType: 'video' | 'pdf' | 'scorm' | 'h5p'
}

export const contentKeys = {
  stream: (id: string) => ['content', 'stream', id] as const,
}

export function useContentStream(contentItemId: string) {
  return useQuery({
    queryKey: contentKeys.stream(contentItemId),
    queryFn: ({ signal }) =>
      apiClient
        .get<ContentStream>(`/api/content/${contentItemId}/stream`, { signal })
        .then(r => r.data),
    enabled: !!contentItemId,
    staleTime: 5 * 60_000,
  })
}

export function useReportContentProgress() {
  return useMutation({
    mutationFn: ({ contentItemId, lessonId, positionSeconds, totalSeconds }: ReportProgressInput) =>
      apiClient
        .post(`/api/content/${contentItemId}/progress`, { positionSeconds, totalSeconds, lessonId })
        .then(() => undefined),
  })
}

export function useUploadContent() {
  return useMutation<ContentItem, Error, UploadContentInput>({
    mutationFn: async ({ file, lessonId, contentType }) => {
      // Step 1: init upload
      const { data: initData } = await apiClient.post<{
        contentItemId: string
        uploadUrl: string
      }>('/api/content/upload', { lessonId, contentType, fileName: file.name, fileSize: file.size })

      // Step 2: upload to S3 directly
      // Direct PUT to S3 pre-signed URL — intentionally bypasses apiClient.
      // Adding an Authorization header would invalidate the AWS signature.
      // The URL is sourced from the trusted LMS API response, not user input.
      // Validate the URL host before upload.
      const uploadUrl = new URL(initData.uploadUrl)
      if (!uploadUrl.hostname.endsWith('.amazonaws.com') && !uploadUrl.hostname.endsWith('localhost')) {
        throw new Error('Unexpected upload URL host')
      }
      await fetch(initData.uploadUrl, {
        method: 'PUT',
        body: file,
        headers: { 'Content-Type': file.type },
      })

      // Step 3: trigger processing
      const { data: item } = await apiClient.post<ContentItem>(
        `/api/content/${initData.contentItemId}/process`,
      )
      return item
    },
  })
}
