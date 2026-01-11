import { useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import type { ConversationDto, UserSearchItemDto } from "@/types/api"

export function NewDirectDialog(props: {
  onCreated?: (conversationId: string) => void

  // controlled open support:
  open?: boolean
  onOpenChange?: (open: boolean) => void

  // if true, do not render default trigger button
  hideTrigger?: boolean
}) {
  const qc = useQueryClient()

  const [innerOpen, setInnerOpen] = useState(false)
  const open = props.open ?? innerOpen
  const setOpen = (v: boolean) => (props.onOpenChange ? props.onOpenChange(v) : setInnerOpen(v))

  const [q, setQ] = useState("")
  const [err, setErr] = useState<string | null>(null)

  const qNorm = useMemo(() => q.trim(), [q])

  const searchQ = useQuery({
    queryKey: ["users.search", qNorm],
    enabled: open && qNorm.length >= 2,
    queryFn: async () => {
      const res = await api.get<UserSearchItemDto[]>("/api/users/search", { params: { q: qNorm, take: 20 } })
      return res.data
    }
  })

  const createDirect = useMutation({
    mutationFn: async (otherUserId: string) => {
      // ConversationType.Direct = 1
      const res = await api.post<ConversationDto>("/api/conversations", {
        type: 1,
        title: "",
        memberIds: [otherUserId]
      })
      return res.data
    },
    onSuccess: async (conv) => {
      await qc.invalidateQueries({ queryKey: ["conversations"] })
      props.onCreated?.(conv.id)
      setErr(null)
      setQ("")
      setOpen(false)
    },
    onError: (ex: any) => {
      const data = ex?.response?.data
      const msg = typeof data === "string" ? data : "Не удалось создать чат."
      setErr(msg)
    }
  })

  const close = () => {
    setErr(null)
    setQ("")
    setOpen(false)
  }

  // Default trigger (optional)
  if (!open && !props.hideTrigger) {
    return (
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        Новый чат
      </Button>
    )
  }

  if (!open) return null

  const results = searchQ.data ?? []

  return (
    <div className="fixed inset-0 bg-black/30 flex items-center justify-center p-6 z-50" onClick={close}>
      <Card className="w-full max-w-lg" onClick={(e) => e.stopPropagation()}>
        <CardHeader className="flex flex-row items-center justify-between gap-2">
          <CardTitle>Новый чат</CardTitle>
          <Button variant="outline" size="sm" onClick={close}>
            Закрыть
          </Button>
        </CardHeader>

        <CardContent className="space-y-3">
          <Input
            value={q}
            onChange={(e) => {
              setQ(e.target.value)
              setErr(null)
            }}
            placeholder="Username (от 2 символов)"
            autoFocus
            onKeyDown={(e) => {
              if (e.key === "Escape") close()
            }}
          />

          {err && <div className="text-sm text-red-600">{err}</div>}

          {qNorm.length >= 2 && (
            <div className="border rounded-xl overflow-hidden">
              <div className="max-h-64 overflow-auto">
                {results.length === 0 && (
                  <div className="px-3 py-2 text-sm text-muted-foreground">
                    {searchQ.isFetching ? "Поиск…" : "Ничего не найдено"}
                  </div>
                )}

                {results.map((u) => (
                  <button
                    key={u.id}
                    className="w-full text-left px-3 py-2 text-sm hover:bg-muted flex items-center justify-between gap-2"
                    onClick={() => {
                      setErr(null)
                      createDirect.mutate(String(u.id))
                    }}
                    disabled={createDirect.isPending}
                  >
                    <span className="truncate">{u.userName}</span>
                    <span className="text-xs text-muted-foreground">{createDirect.isPending ? "…" : ""}</span>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="text-xs text-muted-foreground">
            Начни вводить username → выбери пользователя. Esc — закрыть.
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
