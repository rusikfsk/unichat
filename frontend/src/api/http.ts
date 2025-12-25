type Tokens = { accessToken: string; refreshToken: string }

const storageKey = 'unichat_tokens'

export function getTokens(): Tokens | null {
  const raw = localStorage.getItem(storageKey)
  if (!raw) return null
  try {
    return JSON.parse(raw) as Tokens
  } catch {
    return null
  }
}

export function setTokens(tokens: Tokens | null) {
  if (!tokens) localStorage.removeItem(storageKey)
  else localStorage.setItem(storageKey, JSON.stringify(tokens))
}

async function refreshTokens(): Promise<string | null> {
  const tokens = getTokens()
  if (!tokens?.refreshToken) return null

  const res = await fetch(`/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: tokens.refreshToken }),
  })

  if (!res.ok) {
    setTokens(null)
    return null
  }

  const data = (await res.json()) as Tokens
  setTokens(data)
  return data.accessToken
}

export async function apiFetch(input: string, init: RequestInit = {}) {
  const tokens = getTokens()

  const headers = new Headers(init.headers)
  if (!headers.has('Content-Type') && init.body) headers.set('Content-Type', 'application/json')
  if (tokens?.accessToken) headers.set('Authorization', `Bearer ${tokens.accessToken}`)

  const doFetch = () => fetch(input, { ...init, headers })

  let res = await doFetch()

  // 401 -> refresh 1 раз
  if (res.status === 401) {
    const newAccess = await refreshTokens()
    if (!newAccess) return res
    headers.set('Authorization', `Bearer ${newAccess}`)
    res = await doFetch()
  }

  return res
}
