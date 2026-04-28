export function AccessDenied() {
  return (
    <div className="flex min-h-[400px] flex-col items-center justify-center gap-4 text-center">
      <h2 className="text-2xl font-semibold text-foreground">Access Denied</h2>
      <p className="text-muted-foreground">
        You do not have permission to view this page.
      </p>
    </div>
  )
}
