import { useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import { api } from "@/lib/api"
import { useAuthStore } from "@/stores/auth"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"

export function LoginPage() {
  const nav = useNavigate()
  const setTokens = useAuthStore((s) => s.setTokens)
  const fetchMe = useAuthStore((s) => s.fetchMe)

  const [userName, setUserName] = useState("")
  const [password, setPassword] = useState("")
  const [err, setErr] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr(null)
    setLoading(true)
    try {
      const res = await api.post("/api/auth/login", { userName, password })
      setTokens({ accessToken: res.data.accessToken, refreshToken: res.data.refreshToken })
      await fetchMe()
      nav("/chats")
    } catch (ex: any) {
      const data = ex?.response?.data
      const msg =
        typeof data === "string"
          ? data
          : data?.message && typeof data.message === "string"
            ? data.message
            : "Unauthorized"
      setErr(msg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Вход</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label>Username</Label>
              <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoComplete="username" />
            </div>
            <div className="space-y-2">
              <Label>Password</Label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
            </div>
            {err && <div className="text-sm text-red-600">{err}</div>}
            <Button type="submit" className="w-full" disabled={loading}>
              Войти
            </Button>
            <div className="text-sm">
              Нет аккаунта? <Link className="underline" to="/register">Регистрация</Link>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
