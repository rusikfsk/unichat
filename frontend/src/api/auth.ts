import { apiFetch, setTokens } from './http'

export type AuthResponse = { accessToken: string; refreshToken: string }

export async function login(userName: string, password: string) {
  const res = await apiFetch('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ userName, password }),
  })
  if (!res.ok) throw new Error(await safeText(res))
  const data = (await res.json()) as AuthResponse
  setTokens(data)
  return data
}

export async function register(userName: string, password: string) {
  const res = await apiFetch('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ userName, password }),
  })
  if (!res.ok) throw new Error(await safeText(res))
  const data = (await res.json()) as AuthResponse
  setTokens(data)
  return data
}

export async function logout() {
  const raw = localStorage.getItem('unichat_tokens')
  if (!raw) return
  const t = JSON.parse(raw) as AuthResponse

  await apiFetch('/api/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken: t.refreshToken }),
  })

  setTokens(null)
}

async function safeText(res: Response) {
  try {
    return await res.text()
  } catch {
    return `HTTP ${res.status}`
  }
}
