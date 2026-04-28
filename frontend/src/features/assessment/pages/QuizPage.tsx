interface Props {
  courseId: string
  assessmentId: string
}

export function QuizPage({ courseId: _courseId, assessmentId }: Props) {
  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-6 text-2xl font-bold text-foreground">Quiz</h1>
      <p className="text-muted-foreground">Quiz runner — coming in T8. Assessment: {assessmentId}</p>
    </div>
  )
}
