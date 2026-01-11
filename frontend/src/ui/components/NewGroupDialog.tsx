import { useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { cn } from "@/lib/utils"
import type { ConversationDto, UserSearchItemDto } from "@/types/api"

function uniqById(list: UserSearchItemDto[]) {
  const map = new Map<string, UserSearchItemDto>()
  for (const u of list) map.set(String(u.id), u)
  return Array.from(map.values())
}

export function NewGroupDialog(props: {
  onCreated?: (conversationId: string) => void

  // controlled open support:
  open?: boolean
  onOpenChange?: (open: boolean) => void

  // if you want to hide default button trigger:
  hideTrigger?: boolean
}) {
  const qc = useQueryClient()

  const [innerOpen, setInnerOpen] = useState(false)
  const open = props.open ?? innerOpen
  const setOpen = (v: boolean) => (props.onOpenChange ? props.onOpenChange(v) : setInnerOpen(v))

  const [title, setTitle] = useState("")
  const [q, setQ] = useState("")
  const [selected, setSelected] = useState<UserSearchItemDto[]>([])
  const [err, setErr] = useState<string | null>(null)

  const titleNorm = useMemo(() => title.trim(), [title])
  const qNorm = useMemo(() => q.trim(), [q])

  const searchQ = useQuery({
    queryKey: ["users.search", qNorm],
    enabled: open && qNorm.length >= 2,
    queryFn: async () => {
      const res = await api.get<UserSearchItemDto[]>("/api/users/search", { params: { q: qNorm, take: 20 } })
      return res.data
    }
  })

  const create = useMutation({
    mutationFn: async () => {
      const memberIds = selected.map((x) => x.id)
      if (!titleNorm) throw new Error("Title is required")
      if (memberIds.length < 1) throw new Error("Pick at least 1 user")

      // ConversationType.Group = 2
      const res = await api.post<ConversationDto>("/api/conversations", {
        type: 2,
        title: titleNorm,
        memberIds
      })
      return res.data
    },
    onSuccess: async (conv) => {
      await qc.invalidateQueries({ queryKey: ["conversations"] })
      props.onCreated?.(conv.id)
      setTitle("")
      setQ("")
      setSelected([])
      setErr(null)
      setOpen(false)
    },
    onError: (ex: any) => {
      const data = ex?.response?.data
      const msg =
        typeof data === "string"
          ? data
          : ex?.message === "Title is required"
          ? "Название группы обязательно."
          : ex?.message === "Pick at least 1 user"
          ? "Добавь хотя бы одного участника."
          : "Не удалось создать группу."
      setErr(msg)
    }
  })

  const resultsRaw = searchQ.data ?? []
  const results = useMemo(() => {
    const sel = new Set(selected.map((x) => String(x.id)))
    return resultsRaw.filter((u) => !sel.has(String(u.id)))
  }, [resultsRaw, selected])

  const canCreate = titleNorm.length >= 2 && selected.length >= 1 && !create.isPending

  const resetAndClose = () => {
    setErr(null)
    setTitle("")
    setQ("")
    setSelected([])
    setOpen(false)
  }

  // Default trigger button (optional)
  if (!open && !props.hideTrigger) {
    return (
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        Новая группа
      </Button>
    )
  }

  if (!open) return null

  return (
    <div className="fixed inset-0 bg-black/30 flex items-center justify-center p-6 z-50" onClick={resetAndClose}>
      <Card className="w-full max-w-lg" onClick={(e) => e.stopPropagation()}>
        <CardHeader>
          <CardTitle>Новая группа</CardTitle>
        </CardHeader>

        <CardContent className="space-y-3">
          <Input
            value={title}
            onChange={(e) => {
              setTitle(e.target.value)
              setErr(null)
            }}
            placeholder="Название группы (от 2 символов)"
            autoFocus
            onKeyDown={(e) => {
              if (e.key === "Escape") resetAndClose()
              if (e.key === "Enter" && canCreate) create.mutate()
            }}
          />

          <div className="space-y-2">
            <div className="text-xs text-muted-foreground">Участники</div>

            {selected.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {selected.map((u) => (
                  <div key={u.id} className="flex items-center gap-2 px-2 py-1 rounded-full border bg-background text-sm">
                    <span className="max-w-[220px] truncate">{u.userName}</span>
                    <button
                      className="text-muted-foreground hover:text-foreground"
                      onClick={() => {
                        setSelected((prev) => prev.filter((x) => String(x.id) !== String(u.id)))
                        setErr(null)
                      }}
                      aria-label={`remove ${u.userName}`}
                      title="Убрать"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-sm text-muted-foreground">Пока никого не добавили</div>
            )}
          </div>

          <Input
            value={q}
            onChange={(e) => {
              setQ(e.target.value)
              setErr(null)
            }}
            placeholder="Добавить участника: username (от 2 символов)"
            onKeyDown={(e) => {
              if (e.key === "Escape") resetAndClose()
            }}
          />

          {qNorm.length >= 2 && (
            <div className="border rounded-md overflow-hidden">
              <div className="max-h-56 overflow-auto">
                {results.length === 0 && (
                  <div className="px-3 py-2 text-sm text-muted-foreground">
                    {searchQ.isFetching ? "Поиск…" : "Ничего не найдено"}
                  </div>
                )}

                {results.map((u) => (
                  <button
                    key={u.id}
                    className="w-full text-left px-3 py-2 text-sm hover:bg-muted"
                    onClick={() => {
                      setSelected((prev) => uniqById([...prev, u]))
                      setQ("")
                      setErr(null)
                    }}
                  >
                    {u.userName}
                  </button>
                ))}
              </div>
            </div>
          )}

          {err && <div className="text-sm text-red-600">{err}</div>}

          <div className="flex items-center justify-end gap-2 pt-1">
            <Button variant="outline" onClick={resetAndClose} disabled={create.isPending}>
              Отмена
            </Button>
            <Button onClick={() => create.mutate()} disabled={!canCreate} className={cn(canCreate ? "" : "opacity-60")}>
              {create.isPending ? "Создаю…" : "Создать"}
            </Button>
          </div>

          <div className="text-xs text-muted-foreground">Enter — создать • Esc — закрыть</div>
        </CardContent>
      </Card>
    </div>
  )
}
