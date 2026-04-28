import { useAuth } from '@/features/auth/hooks/useAuth'

export function NavBar() {
  const { isAuthenticated, hasRole } = useAuth()

  return (
    <nav className="flex items-center gap-6 text-sm">
      <a href="/courses" className="text-foreground/80 hover:text-foreground">
        Courses
      </a>
      {isAuthenticated && (
        <a href="/dashboard" className="text-foreground/80 hover:text-foreground">
          Dashboard
        </a>
      )}
      {isAuthenticated && (
        <a href="/certificates" className="text-foreground/80 hover:text-foreground">
          Certificates
        </a>
      )}
      {isAuthenticated && (hasRole('instructor') || hasRole('admin')) && (
        <a href="/instructor/courses" className="text-foreground/80 hover:text-foreground">
          Instructor
        </a>
      )}
    </nav>
  )
}
