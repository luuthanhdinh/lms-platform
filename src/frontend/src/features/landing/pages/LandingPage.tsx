export function LandingPage() {
  return (
    <main className="mx-auto max-w-7xl px-4 py-20 text-center">
      <h1 className="mb-4 text-5xl font-bold tracking-tight text-foreground">
        Learn Without Limits
      </h1>
      <p className="mb-8 text-xl text-muted-foreground">
        Access high-quality courses from expert instructors.
      </p>
      <a
        href="/courses"
        className="inline-flex items-center rounded-md bg-primary px-6 py-3 text-base font-medium text-primary-foreground hover:bg-primary/90"
      >
        Browse Courses
      </a>
    </main>
  )
}
