import { create } from 'zustand'
import type { AnswerDto } from '@/types'

interface QuizState {
  sessionId: string | null
  currentQuestionIndex: number
  draftAnswers: Record<string, AnswerDto>
  startedAt: string | null
  expiresAt: string | null
  setSession: (sessionId: string, expiresAt: string) => void
  setAnswer: (questionId: string, answer: AnswerDto) => void
  next: () => void
  prev: () => void
  reset: () => void
}

export const useQuizStore = create<QuizState>(set => ({
  sessionId: null,
  currentQuestionIndex: 0,
  draftAnswers: {},
  startedAt: null,
  expiresAt: null,
  setSession: (sessionId, expiresAt) =>
    set({ sessionId, expiresAt, startedAt: new Date().toISOString(), currentQuestionIndex: 0, draftAnswers: {} }),
  setAnswer: (questionId, answer) =>
    set(state => ({ draftAnswers: { ...state.draftAnswers, [questionId]: answer } })),
  next: () => set(state => ({ currentQuestionIndex: state.currentQuestionIndex + 1 })),
  prev: () => set(state => ({ currentQuestionIndex: Math.max(0, state.currentQuestionIndex - 1) })),
  reset: () =>
    set({ sessionId: null, currentQuestionIndex: 0, draftAnswers: {}, startedAt: null, expiresAt: null }),
}))
