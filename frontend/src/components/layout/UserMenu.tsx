import { useAuth } from '@/features/auth/hooks/useAuth'

export function UserMenu() {
  const { isAuthenticated, login, logout, userId } = useAuth()

  if (!isAuthenticated) {
    return (
      <button
        onClick={login}
        className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
      >
        Sign in
      </button>
    )
  }

  return (
    <div className="flex items-center gap-3">
      <span className="text-sm text-muted-foreground">{userId}</span>
      <button
        onClick={logout}
        className="rounded-md border border-border px-3 py-1.5 text-sm hover:bg-accent"
      >
        Sign out
      </button>
    </div>
  )
}
