interface Props {
  message?: string
}

export function ErrorBanner({ message = 'Something went wrong. Please try again.' }: Props) {
  return (
    <div className="rounded-md border border-destructive/50 bg-destructive/10 p-4 text-destructive">
      <p>{message}</p>
    </div>
  )
}
