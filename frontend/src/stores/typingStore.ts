import { create } from "zustand"

type TypingState = {
  byConversationId: Record<string, string[]>
  setTyping: (conversationId: string, userId: string) => void
  clearTyping: (conversationId: string, userId: string) => void
  clearConversation: (conversationId: string) => void
}

export const useTypingStore = create<TypingState>((set) => ({
  byConversationId: {},
  setTyping: (conversationId, userId) =>
    set((s) => {
      const prev = s.byConversationId[conversationId] ?? []
      if (prev.includes(userId)) return s
      return { byConversationId: { ...s.byConversationId, [conversationId]: [...prev, userId] } }
    }),
  clearTyping: (conversationId, userId) =>
    set((s) => {
      const prev = s.byConversationId[conversationId] ?? []
      const next = prev.filter((x) => x !== userId)
      return { byConversationId: { ...s.byConversationId, [conversationId]: next } }
    }),
  clearConversation: (conversationId) =>
    set((s) => {
      const copy = { ...s.byConversationId }
      delete copy[conversationId]
      return { byConversationId: copy }
    })
}))
