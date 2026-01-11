import { useMemo, useState } from "react"
import { useSearchParams, Link } from "react-router-dom"
import { api } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"

export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const userId = params.get("userId") ?? ""
  const token = params.get("token") ?? ""

  const canSend = useMemo(() => userId.length > 0 && token.length > 0, [userId, token])

  const [status, setStatus] = useState<"idle" | "ok" | "err">("idle")
  const [loading, setLoading] = useState(false)

  const confirm = async () => {
    setLoading(true)
    try {
      await api.post("/api/auth/confirm-email", { userId, token })
      setStatus("ok")
    } catch {
      setStatus("err")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Подтверждение email</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {!canSend && <div className="text-sm">Нет параметров userId/token в URL.</div>}
          {status === "ok" && <div className="text-sm text-green-700">Email подтверждён. Теперь можно войти.</div>}
          {status === "err" && <div className="text-sm text-red-600">Не удалось подтвердить. Проверь ссылку.</div>}
          <Button onClick={confirm} disabled={!canSend || loading} className="w-full">
            Подтвердить
          </Button>
          <div className="text-sm">
            <Link className="underline" to="/login">Перейти к входу</Link>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
