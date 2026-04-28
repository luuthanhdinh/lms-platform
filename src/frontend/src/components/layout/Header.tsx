import { NavBar } from './NavBar'
import { UserMenu } from './UserMenu'

export function Header() {
  return (
    <header className="sticky top-0 z-50 w-full border-b border-border bg-background/95 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-7xl items-center justify-between px-4">
        <div className="flex items-center gap-8">
          <a href="/" className="text-lg font-semibold text-foreground">
            LMS
          </a>
          <NavBar />
        </div>
        <UserMenu />
      </div>
    </header>
  )
}
