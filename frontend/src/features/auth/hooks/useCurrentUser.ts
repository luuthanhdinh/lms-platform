import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { useAuth } from './useAuth'

interface UserProfile {
  id: string
  email: string
  firstName: string
  lastName: string
  tenantId: string
  roles: string[]
}

export function useCurrentUser() {
  const { isAuthenticated } = useAuth()

  return useQuery({
    queryKey: ['identity', 'me'],
    queryFn: ({ signal }) =>
      apiClient.get<UserProfile>('/api/identity/me', { signal }).then(r => r.data),
    enabled: isAuthenticated,
  })
}
