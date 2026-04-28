import { useEffect } from 'react'
import { AccessDenied } from '@/components/feedback/AccessDenied'
import { useAuth } from '../hooks/useAuth'

interface Props {
  children: React.ReactNode
  roles?: string[]
}

export function ProtectedRoute({ children, roles }: Props) {
  const { isAuthenticated, hasRole, login } = useAuth()

  useEffect(() => {
    if (!isAuthenticated) {
      login()
    }
  }, [isAuthenticated, login])

  if (!isAuthenticated) return null

  if (roles && !roles.some(r => hasRole(r))) {
    return <AccessDenied />
  }

  return <>{children}</>
}
