import { create } from 'zustand'

interface Toast {
  id: string
  title: string
  description?: string
  variant?: 'default' | 'destructive'
}

interface UiState {
  sidebarOpen: boolean
  theme: 'light' | 'dark' | 'system'
  toasts: Toast[]
  setSidebarOpen: (open: boolean) => void
  setTheme: (theme: 'light' | 'dark' | 'system') => void
  addToast: (toast: Omit<Toast, 'id'>) => void
  removeToast: (id: string) => void
}

export const useUiStore = create<UiState>(set => ({
  sidebarOpen: false,
  theme: 'system',
  toasts: [],
  setSidebarOpen: open => set({ sidebarOpen: open }),
  setTheme: theme => set({ theme }),
  addToast: toast =>
    set(state => ({
      toasts: [...state.toasts, { ...toast, id: crypto.randomUUID() }],
    })),
  removeToast: id =>
    set(state => ({ toasts: state.toasts.filter(t => t.id !== id) })),
}))
