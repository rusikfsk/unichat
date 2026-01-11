import { create } from "zustand"
import { setTokens, getTokens, type Tokens } from "@/lib/tokens"
import { api } from "@/lib/api"

export type AuthUser = { id: string; userName: string; createdAt: string }

type AuthState = {
  tokens: Tokens | null
  user: AuthUser | null
  setTokens: (t: Tokens | null) => void
  fetchMe: () => Promise<void>
  logout: () => Promise<void>
}

export const useAuthStore = create<AuthState>((set, get) => ({
  tokens: getTokens(),
  user: null,

  setTokens: (t) => {
    setTokens(t)
    set({ tokens: t })
  },

  fetchMe: async () => {
    const { tokens } = get()
    if (!tokens?.accessToken) {
      set({ user: null })
      return
    }
    const res = await api.get<AuthUser>("/api/auth/me")
    set({ user: res.data })
  },

  logout: async () => {
    const t = get().tokens
    try {
      if (t?.refreshToken) await api.post("/api/auth/logout", { refreshToken: t.refreshToken })
    } finally {
      setTokens(null)
      set({ tokens: null, user: null })
    }
  }
}))
