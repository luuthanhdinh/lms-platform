interface Props {
  title?: string
  description?: string
}

export function EmptyState({
  title = 'Nothing here yet',
  description = 'Check back later.',
}: Props) {
  return (
    <div className="flex min-h-[200px] flex-col items-center justify-center gap-2 text-center">
      <h3 className="text-lg font-medium text-foreground">{title}</h3>
      <p className="text-sm text-muted-foreground">{description}</p>
    </div>
  )
}
