import axios from "axios"
import { env } from "@/lib/env"
import { getTokens, setTokens, type Tokens } from "@/lib/tokens"

export const api = axios.create({
  baseURL: env.apiUrl
})

let refreshing: Promise<Tokens> | null = null

api.interceptors.request.use((config) => {
  const tokens = getTokens()
  if (tokens?.accessToken) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${tokens.accessToken}`
  }
  return config
})

api.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error?.config
    const status = error?.response?.status

    if (status !== 401 || !original || original._retry) throw error
    original._retry = true

    const tokens = getTokens()
    if (!tokens?.refreshToken) {
      setTokens(null)
      throw error
    }

    if (!refreshing) {
      refreshing = api
        .post("/api/auth/refresh", { refreshToken: tokens.refreshToken })
        .then((res) => {
          const next: Tokens = { accessToken: res.data.accessToken, refreshToken: res.data.refreshToken }
          setTokens(next)
          return next
        })
        .finally(() => {
          refreshing = null
        })
    }

    const next = await refreshing
    original.headers = original.headers ?? {}
    original.headers.Authorization = `Bearer ${next.accessToken}`
    return api.request(original)
  }
)
