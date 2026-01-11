export type Tokens = { accessToken: string; refreshToken: string }

const KEY = "unichat_tokens"

export function getTokens(): Tokens | null {
  const raw = localStorage.getItem(KEY)
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as Tokens
    if (!parsed?.accessToken || !parsed?.refreshToken) return null
    return parsed
  } catch {
    return null
  }
}

export function setTokens(tokens: Tokens | null) {
  if (!tokens) localStorage.removeItem(KEY)
  else localStorage.setItem(KEY, JSON.stringify(tokens))
}
