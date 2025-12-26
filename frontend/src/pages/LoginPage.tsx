import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login, register } from '../api/auth'
import styles from './LoginPage.module.css'

export function LoginPage() {
  const nav = useNavigate()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    setBusy(true)
    try {
      if (mode === 'login') await login(userName, password)
      else await register(userName, password)
      nav('/chats')
    } catch (e: any) {
      setErr(e?.message ?? 'Error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={styles.shell}>
      <div className={styles.page}>
        <div className={styles.card}>
          <div className={styles.brand}>
            <div className={styles.logo}>U</div>
            <div>
              <div className={styles.title}>UniChat</div>
              <div className={styles.sub}>Sign in to continue</div>
            </div>
          </div>

          <div className={styles.tabs}>
            <button
              type="button"
              className={mode === 'login' ? styles.tabActive : styles.tab}
              onClick={() => setMode('login')}
            >
              Login
            </button>

            <button
              type="button"
              className={mode === 'register' ? styles.tabActive : styles.tab}
              onClick={() => setMode('register')}
            >
              Register
            </button>
          </div>

          <form onSubmit={onSubmit} className={styles.form}>
            <label className={styles.label}>Username</label>
            <input
              className={styles.input}
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
              placeholder="yourname"
              autoComplete="username"
            />

            <label className={styles.label}>Password</label>
            <input
              className={styles.input}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            />

            {err && <div className={styles.error}>{err}</div>}

            <button className={styles.primary} disabled={busy}>
              {busy ? 'Please wait…' : mode === 'login' ? 'Login' : 'Create account'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}
