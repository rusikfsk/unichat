import { useState } from "react"
import { Link } from "react-router-dom"
import { api } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"

export function RegisterPage() {
  const [userName, setUserName] = useState("")
  const [email, setEmail] = useState("")
  const [displayName, setDisplayName] = useState("")
  const [password, setPassword] = useState("")
  const [ok, setOk] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr(null)
    setOk(false)
    setLoading(true)
    try {
      await api.post("/api/auth/register", { userName, email, displayName, password })
      setOk(true)
    } catch (ex: any) {
    const data = ex?.response?.data
    const msg =
      typeof data === "string"
        ? data
        : data?.message && typeof data.message === "string"
          ? data.message
          : "Не удалось зарегистрироваться."
    setErr(msg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Регистрация</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label>Username</Label>
              <Input value={userName} onChange={(e) => setUserName(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Display name</Label>
              <Input value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Password</Label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
            {ok && <div className="text-sm text-green-700">Аккаунт создан. Проверь email и перейди по ссылке подтверждения.</div>}
            {err && <div className="text-sm text-red-600">{err}</div>}
            <Button type="submit" className="w-full" disabled={loading}>
              Зарегистрироваться
            </Button>
            <div className="text-sm">
              Уже есть аккаунт? <Link className="underline" to="/login">Вход</Link>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
