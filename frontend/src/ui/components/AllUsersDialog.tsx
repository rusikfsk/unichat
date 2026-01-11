import { useEffect, useMemo, useRef, useState } from "react"
import { useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import type { UserDto, ConversationDto } from "@/types/api"
import { cn } from "@/lib/utils"

type UsersPage = {
  items: UserDto[]
  skip: number
  take: number
  hasMore: boolean
}

function useDebouncedValue<T>(value: T, delayMs: number) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = window.setTimeout(() => setV(value), delayMs)
    return () => window.clearTimeout(t)
  }, [value, delayMs])
  return v
}

export function AllUsersDialog(props: {
  meId: string
  onOpenConversation?: (conversationId: string) => void

  open?: boolean
  onOpenChange?: (open: boolean) => void
  hideTrigger?: boolean
}) {
  const qc = useQueryClient()

  const [innerOpen, setInnerOpen] = useState(false)
  const open = props.open ?? innerOpen
  const setOpen = (v: boolean) => (props.onOpenChange ? props.onOpenChange(v) : setInnerOpen(v))

  const [q, setQ] = useState("")
  const qDebounced = useDebouncedValue(q, 300)
  const [err, setErr] = useState<string | null>(null)

  const TAKE = 50
  const meId = String(props.meId)

  const usersQ = useInfiniteQuery({
    queryKey: ["users.all", qDebounced],
    enabled: open,
    initialPageParam: 0,
    queryFn: async ({ pageParam }) => {
      const skip = Number(pageParam ?? 0)
      const res = await api.get<UserDto[]>("/api/users", {
        params: {
          skip,
          take: TAKE,
          q: qDebounced.trim() || undefined
        }
      })
      const items = res.data ?? []
      const hasMore = items.length === TAKE
      const page: UsersPage = { items, skip, take: TAKE, hasMore }
      return page
    },
    getNextPageParam: (lastPage) => (lastPage.hasMore ? lastPage.skip + lastPage.take : undefined),
    retry: false
  })

  const flatUsers = useMemo(() => {
    const pages = usersQ.data?.pages ?? []
    const all = pages.flatMap((p) => p.items)
    const filtered = all.filter((u) => String(u.id) !== meId)
    const map = new Map<string, UserDto>()
    for (const u of filtered) map.set(String(u.id), u)
    return Array.from(map.values())
  }, [usersQ.data, meId])

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
      props.onOpenConversation?.(conv.id)
      setOpen(false)
    },
    onError: (ex: any) => {
      const data = ex?.response?.data
      const msg = typeof data === "string" ? data : "Не удалось создать диалог."
      setErr(msg)
    }
  })

  const close = () => {
    setOpen(false)
    setQ("")
    setErr(null)
  }

  // infinite scroll observer
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  useEffect(() => {
    if (!open) return
    const el = sentinelRef.current
    if (!el) return

    const io = new IntersectionObserver(
      (entries) => {
        const first = entries[0]
        if (!first?.isIntersecting) return
        if (usersQ.hasNextPage && !usersQ.isFetchingNextPage) {
          usersQ.fetchNextPage()
        }
      },
      { root: null, rootMargin: "200px", threshold: 0 }
    )

    io.observe(el)
    return () => io.disconnect()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, usersQ.hasNextPage, usersQ.isFetchingNextPage, qDebounced])

  useEffect(() => {
    setErr(null)
  }, [qDebounced])

  // optional default trigger
  if (!open && !props.hideTrigger) {
    return (
      <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
        Пользователи
      </Button>
    )
  }
  if (!open) return null

  const initialLoading = usersQ.isLoading
  const fetchingMore = usersQ.isFetchingNextPage
  const apiError =
    usersQ.isError ? (usersQ.error as any)?.response?.data ?? "Не удалось загрузить пользователей." : null

  return (
    <div className="fixed inset-0 bg-black/30 flex items-center justify-center p-6 z-50" onClick={close}>
      <Card className="w-full max-w-xl" onClick={(e) => e.stopPropagation()}>
        <CardHeader className="flex flex-row items-center justify-between gap-2">
          <CardTitle>Все пользователи</CardTitle>
          <Button variant="outline" size="sm" onClick={close}>
            Закрыть
          </Button>
        </CardHeader>

        <CardContent className="space-y-3">
          <Input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Поиск по username (серверный)"
            onKeyDown={(e) => {
              if (e.key === "Escape") close()
            }}
          />

          {err && <div className="text-sm text-red-600">{err}</div>}
          {apiError && <div className="text-sm text-red-600">{String(apiError)}</div>}

          <div className="border rounded-xl overflow-hidden">
            <div className="max-h-[60vh] overflow-auto">
              {initialLoading && <div className="px-3 py-2 text-sm text-muted-foreground">Загрузка…</div>}

              {!initialLoading && flatUsers.length === 0 && !apiError && (
                <div className="px-3 py-2 text-sm text-muted-foreground">Ничего не найдено</div>
              )}

              {flatUsers.map((u) => (
                <div key={u.id} className="px-3 py-2 flex items-center justify-between gap-3 hover:bg-muted">
                  <div className="min-w-0">
                    <div className="text-sm font-medium truncate">{u.userName}</div>
                    <div className="text-xs text-muted-foreground truncate">
                      Registered: {new Date(u.createdAt).toLocaleDateString()}
                    </div>
                  </div>

                  <Button
                    size="sm"
                    variant="outline"
                    disabled={createDirect.isPending}
                    className={cn(createDirect.isPending ? "opacity-60" : "")}
                    onClick={() => {
                      setErr(null)
                      createDirect.mutate(String(u.id))
                    }}
                  >
                    Написать
                  </Button>
                </div>
              ))}

              <div ref={sentinelRef} />
              {fetchingMore && <div className="px-3 py-2 text-sm text-muted-foreground">Загрузка ещё…</div>}

              {!usersQ.isFetching && usersQ.hasNextPage === false && flatUsers.length > 0 && (
                <div className="px-3 py-2 text-xs text-muted-foreground">Конец списка</div>
              )}
            </div>
          </div>

          <div className="text-xs text-muted-foreground">Прокрути вниз — подгрузятся ещё • Esc — закрыть</div>
        </CardContent>
      </Card>
    </div>
  )
}
