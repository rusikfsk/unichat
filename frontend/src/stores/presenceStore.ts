import { create } from "zustand"

type PresenceState = {
  onlineUserIds: Set<string>
  lastSeenByUserId: Record<string, string | null>
  applySnapshot: (userIds: string[]) => void
  setOnline: (userId: string) => void
  setOffline: (userId: string, lastSeenAt?: string | null) => void
  reset: () => void
}

export const usePresenceStore = create<PresenceState>((set) => ({
  onlineUserIds: new Set(),
  lastSeenByUserId: {},

  applySnapshot: (userIds) =>
    set(() => ({
      onlineUserIds: new Set(userIds),
      lastSeenByUserId: {}
    })),

  setOnline: (userId) =>
    set((s) => {
      const next = new Set(s.onlineUserIds)
      next.add(userId)
      const lastSeenByUserId = { ...s.lastSeenByUserId, [userId]: null }
      return { onlineUserIds: next, lastSeenByUserId }
    }),

  setOffline: (userId, lastSeenAt) =>
    set((s) => {
      const next = new Set(s.onlineUserIds)
      next.delete(userId)
      const lastSeenByUserId = { ...s.lastSeenByUserId, [userId]: lastSeenAt ?? s.lastSeenByUserId[userId] ?? null }
      return { onlineUserIds: next, lastSeenByUserId }
    }),

  reset: () => set({ onlineUserIds: new Set(), lastSeenByUserId: {} })
}))
