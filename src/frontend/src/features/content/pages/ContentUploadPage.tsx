export function ContentUploadPage({ courseId }: { courseId: string }) {
  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-6 text-2xl font-bold text-foreground">Upload Content</h1>
      <p className="text-muted-foreground">Upload dialog — coming in T7. Course: {courseId}</p>
    </div>
  )
}
