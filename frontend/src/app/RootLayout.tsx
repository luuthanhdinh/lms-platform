import { Header } from '@/components/layout/Header'

interface Props {
  children: React.ReactNode
}

export function RootLayout({ children }: Props) {
  return (
    <div className="min-h-screen bg-background">
      <Header />
      <main className="mx-auto max-w-7xl px-4 py-8">{children}</main>
    </div>
  )
}
