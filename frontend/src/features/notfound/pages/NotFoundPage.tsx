export function NotFoundPage() {
  return (
    <main className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
      <h1 className="text-6xl font-bold text-muted-foreground">404</h1>
      <h2 className="text-2xl font-semibold text-foreground">Page not found</h2>
      <p className="text-muted-foreground">The page you are looking for does not exist.</p>
      <a href="/" className="text-primary hover:underline">
        Go home
      </a>
    </main>
  )
}
