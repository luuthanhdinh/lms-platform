import { create } from 'zustand'

interface PlayerState {
  currentLessonId: string | null
  isPlaying: boolean
  volume: number
  playbackRate: number
  lastReportedAt: number | null
  setLesson: (id: string) => void
  setPlaying: (v: boolean) => void
  setVolume: (v: number) => void
  setPlaybackRate: (v: number) => void
  setLastReportedAt: (t: number) => void
}

export const usePlayerStore = create<PlayerState>(set => ({
  currentLessonId: null,
  isPlaying: false,
  volume: 1,
  playbackRate: 1,
  lastReportedAt: null,
  setLesson: id => set({ currentLessonId: id }),
  setPlaying: v => set({ isPlaying: v }),
  setVolume: v => set({ volume: v }),
  setPlaybackRate: v => set({ playbackRate: v }),
  setLastReportedAt: t => set({ lastReportedAt: t }),
}))
