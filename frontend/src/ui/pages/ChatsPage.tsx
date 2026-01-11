import { useEffect, useMemo, useRef, useState } from "react"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { AttachmentDto, ConversationListItemDto, MessageDto } from "@/types/api"
import { useAuthStore } from "@/stores/auth"
import { useChatStore } from "@/stores/chat"
import { ensureHubConnected, getHub } from "@/lib/signalr"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Separator } from "@/components/ui/separator"
import { NewDirectDialog } from "@/ui/components/NewDirectDialog"
import { NewGroupDialog } from "@/ui/components/NewGroupDialog"
import { AllUsersDialog } from "@/ui/components/AllUsersDialog"
import { cn } from "@/lib/utils"
import { ConversationTitle } from "@/ui/components/ConversationTitle"
import { TypingController } from "@/lib/typingController"
import { useTypingStore } from "@/stores/typingStore"
import { TypingText } from "@/ui/components/TypingText"
import { usePresenceStore } from "@/stores/presenceStore"
import { MessageBubble } from "@/ui/components/MessageBubble"
import {
  Paperclip,
  Undo2,
  X,
  UploadCloud,
  ChevronLeft,
  ChevronRight,
  Download,
  Image as ImageIcon,
  Menu
} from "lucide-react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@/components/ui/dropdown-menu"

type PendingDelete = {
  message: MessageDto
  index: number
  startedAtMs: number
  timeoutId: number
}

type LightboxItem = { id: string; fileName: string }
type LightboxState = { items: LightboxItem[]; index: number } | null

function formatBytes(bytes: number) {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B"
  const units = ["B", "KB", "MB", "GB"]
  let i = 0
  let n = bytes
  while (n >= 1024 && i < units.length - 1) {
    n /= 1024
    i++
  }
  return `${n.toFixed(i === 0 ? 0 : 1)} ${units[i]}`
}

export function ChatsPage() {
  const qc = useQueryClient()
  const logout = useAuthStore((s) => s.logout)
  const user = useAuthStore((s) => s.user)
  const meId = user?.id ?? ""

  const activeConversationId = useChatStore((s) => s.activeConversationId)
  const setActiveConversationId = useChatStore((s) => s.setActiveConversationId)

  const [sidebarQuery, setSidebarQuery] = useState("")
  const [text, setText] = useState("")
  const [sending, setSending] = useState(false)
  const [replyTo, setReplyTo] = useState<MessageDto | null>(null)

  // burger -> modals
  const [directOpen, setDirectOpen] = useState(false)
  const [groupOpen, setGroupOpen] = useState(false)
  const [usersOpen, setUsersOpen] = useState(false)

  const typingControllerRef = useRef<TypingController | null>(null)
  const setTyping = useTypingStore((s) => s.setTyping)
  const clearTyping = useTypingStore((s) => s.clearTyping)
  const typingByConversationId = useTypingStore((s) => s.byConversationId)

  const applySnapshot = usePresenceStore((s) => s.applySnapshot)
  const setOnline = usePresenceStore((s) => s.setOnline)
  const setOffline = usePresenceStore((s) => s.setOffline)

  // ===== attachments compose =====
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const [attachments, setAttachments] = useState<AttachmentDto[]>([])
  const [uploading, setUploading] = useState(false)
  const [uploadErr, setUploadErr] = useState<string | null>(null)

  // ===== image preview cache (attachmentId -> objectURL) =====
  const [previewUrlByAttachmentId, setPreviewUrlByAttachmentId] = useState<Record<string, string>>({})
  const previewUrlByAttachmentIdRef = useRef<Record<string, string>>({})
  useEffect(() => {
    previewUrlByAttachmentIdRef.current = previewUrlByAttachmentId
  }, [previewUrlByAttachmentId])

  useEffect(() => {
    return () => {
      const map = previewUrlByAttachmentIdRef.current
      for (const k of Object.keys(map)) URL.revokeObjectURL(map[k])
    }
  }, [])

  // ===== Drag & Drop =====
  const [dragging, setDragging] = useState(false)
  const dragDepthRef = useRef(0)

  // ===== delete with undo =====
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null)
  const [undoLeftMs, setUndoLeftMs] = useState(0)

  // ===== lightbox =====
  const [lightbox, setLightbox] = useState<LightboxState>(null)

  useEffect(() => {
    setAttachments([])
    setUploadErr(null)
    setReplyTo(null)
    setLightbox(null)
  }, [activeConversationId])

  useEffect(() => {
    if (!pendingDelete) {
      setUndoLeftMs(0)
      return
    }
    const tick = window.setInterval(() => {
      const left = Math.max(0, 5000 - (Date.now() - pendingDelete.startedAtMs))
      setUndoLeftMs(left)
      if (left <= 0) window.clearInterval(tick)
    }, 100)
    return () => window.clearInterval(tick)
  }, [pendingDelete])

  const convsQ = useQuery({
    queryKey: ["conversations"],
    queryFn: async () => {
      const res = await api.get<ConversationListItemDto[]>("/api/conversations")
      return res.data
    }
  })

  const convsAll = convsQ.data ?? []

  const convs = useMemo(() => {
    const q = sidebarQuery.trim().toLowerCase()
    if (!q) return convsAll
    return convsAll.filter(
      (c) => (c.title ?? "").toLowerCase().includes(q) || (c.lastMessageText ?? "").toLowerCase().includes(q)
    )
  }, [convsAll, sidebarQuery])

  useEffect(() => {
    if (!activeConversationId && convsAll.length > 0) setActiveConversationId(convsAll[0].id)
  }, [activeConversationId, convsAll, setActiveConversationId])

  const active = useMemo(() => {
    if (!activeConversationId) return null
    return convsAll.find((c) => c.id === activeConversationId) ?? null
  }, [convsAll, activeConversationId])

  const msgsQ = useQuery({
    queryKey: ["messages", activeConversationId],
    enabled: !!activeConversationId,
    queryFn: async () => {
      const res = await api.get<MessageDto[]>("/api/messages", {
        params: { conversationId: activeConversationId, take: 50 }
      })
      return res.data
    }
  })

  useEffect(() => {
    const run = async () => {
      await ensureHubConnected()
      const hub = getHub()

      if (!typingControllerRef.current) {
        typingControllerRef.current = new TypingController(hub, () => (activeConversationId ? activeConversationId : null))
      }

      hub.off("message")
      hub.off("message_deleted")
      hub.off("conversation_updated")
      hub.off("read")
      hub.off("typing")
      hub.off("stop_typing")
      hub.off("presence_snapshot")
      hub.off("presence_online")
      hub.off("presence_offline")

      hub.on("presence_snapshot", (p: { onlineUserIds: string[] }) => {
        applySnapshot((p?.onlineUserIds ?? []).map(String))
      })

      hub.on("presence_online", (p: { userId: string }) => {
        const id = String(p.userId)
        if (id !== meId) setOnline(id)
      })

      hub.on("presence_offline", (p: { userId: string; lastSeenAt?: string }) => {
        const id = String(p.userId)
        if (id !== meId) setOffline(id, p.lastSeenAt ? String(p.lastSeenAt) : null)
      })

      hub.on("message", (dto: MessageDto) => {
        qc.setQueryData<MessageDto[]>(["messages", dto.conversationId], (old) => {
          const prev = old ?? []
          if (prev.some((m) => m.id === dto.id)) return prev
          return [...prev, dto]
        })
        qc.invalidateQueries({ queryKey: ["conversations"] })
      })

      hub.on("message_deleted", (e: { conversationId: string; messageId: string }) => {
        qc.setQueryData<MessageDto[]>(["messages", e.conversationId], (old) =>
          (old ?? []).filter((m) => m.id !== e.messageId)
        )
        qc.invalidateQueries({ queryKey: ["conversations"] })
      })

      hub.on("conversation_updated", () => {
        qc.invalidateQueries({ queryKey: ["conversations"] })
      })

      hub.on("read", () => {
        qc.invalidateQueries({ queryKey: ["conversations"] })
      })

      hub.on("typing", (p: { conversationId: string; userId: string }) => {
        setTyping(String(p.conversationId), String(p.userId))
      })

      hub.on("stop_typing", (p: { conversationId: string; userId: string }) => {
        clearTyping(String(p.conversationId), String(p.userId))
      })
    }

    run()

    return () => {
      typingControllerRef.current?.dispose()
      typingControllerRef.current = null
    }
  }, [qc, setTyping, clearTyping, activeConversationId, applySnapshot, setOnline, setOffline, meId])

  useEffect(() => {
    const run = async () => {
      if (!activeConversationId) return
      await ensureHubConnected()
      const hub = getHub()
      await hub.invoke("JoinConversation", activeConversationId)
      setReplyTo(null)
    }
    run()
  }, [activeConversationId])

  useEffect(() => {
    const run = async () => {
      if (!activeConversationId) return
      if (msgsQ.isFetching) return
      const msgs = msgsQ.data ?? []
      if (msgs.length === 0) return
      const lastMessageId = msgs[msgs.length - 1].id
      await api.post("/api/messages/read", { conversationId: activeConversationId, lastMessageId })
      qc.invalidateQueries({ queryKey: ["conversations"] })
    }
    run()
  }, [activeConversationId, msgsQ.isFetching, msgsQ.data, qc])

  const hasTyping = (conversationId: string) => {
    const ids = typingByConversationId[conversationId] ?? []
    return ids.some((x) => x && x !== meId)
  }

  const jumpTo = (messageId: string) => {
    const el = document.getElementById(`msg-${messageId}`)
    if (!el) return
    el.scrollIntoView({ behavior: "smooth", block: "center" })
  }

  const downloadAttachment = async (attachmentId: string, fileName: string) => {
    try {
      const res = await api.get(`/api/attachments/${attachmentId}`, { responseType: "blob" })
      const blob = res.data as Blob
      const url = URL.createObjectURL(blob)
      const a = document.createElement("a")
      a.href = url
      a.download = fileName || "file"
      document.body.appendChild(a)
      a.click()
      a.remove()
      URL.revokeObjectURL(url)
    } catch {
      alert("Не удалось скачать файл.")
    }
  }

  const ensureImagePreview = async (attachmentId: string, fileName: string) => {
    if (previewUrlByAttachmentIdRef.current[attachmentId]) return
    try {
      const res = await api.get(`/api/attachments/${attachmentId}`, { responseType: "blob" })
      const blob = res.data as Blob
      const url = URL.createObjectURL(blob)
      setPreviewUrlByAttachmentId((prev) => {
        if (prev[attachmentId]) {
          URL.revokeObjectURL(url)
          return prev
        }
        return { ...prev, [attachmentId]: url }
      })
    } catch {
      // ignore
    }
  }

  const uploadFiles = async (files: FileList | null) => {
    if (!files || files.length === 0) return
    setUploadErr(null)
    setUploading(true)
    try {
      const uploaded: AttachmentDto[] = []
      for (const f of Array.from(files)) {
        const fd = new FormData()
        fd.append("File", f)
        const res = await api.post<AttachmentDto>("/api/attachments", fd, {
          headers: { "Content-Type": "multipart/form-data" }
        })
        uploaded.push(res.data)
      }

      setAttachments((prev) => {
        const next = [...prev]
        for (const a of uploaded) if (!next.some((x) => x.id === a.id)) next.push(a)
        return next
      })
    } catch (ex: any) {
      const data = ex?.response?.data
      const msg = typeof data === "string" ? data : "Не удалось загрузить файл."
      setUploadErr(msg)
    } finally {
      setUploading(false)
      if (fileInputRef.current) fileInputRef.current.value = ""
    }
  }

  const removeAttachment = (id: string) => {
    setAttachments((prev) => prev.filter((x) => x.id !== id))
    setPreviewUrlByAttachmentId((prev) => {
      const url = prev[id]
      if (url) URL.revokeObjectURL(url)
      const next = { ...prev }
      delete next[id]
      return next
    })
  }

  useEffect(() => {
    const imgs = attachments.filter((a) => (a.contentType ?? "").toLowerCase().startsWith("image/"))
    for (const a of imgs) {
      if (!previewUrlByAttachmentId[a.id]) ensureImagePreview(a.id, a.fileName)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attachments])

  const send = async () => {
    if (!activeConversationId) return
    const t = text.trim()
    const hasText = t.length > 0
    const hasAtts = attachments.length > 0
    if (!hasText && !hasAtts) return

    typingControllerRef.current?.onStop()
    setSending(true)
    try {
      await api.post("/api/messages", {
        conversationId: activeConversationId,
        text: hasText ? t : "",
        attachmentIds: hasAtts ? attachments.map((a) => a.id) : [],
        replyToMessageId: replyTo?.id ?? null
      })
      setText("")
      setReplyTo(null)
      setAttachments([])
      setUploadErr(null)
    } finally {
      setSending(false)
    }
  }

  const scheduleDeleteWithUndo = (m: MessageDto) => {
    if (!meId || m.senderId !== meId) return
    if (pendingDelete) return

    const listKey = ["messages", m.conversationId] as const
    const prev = qc.getQueryData<MessageDto[]>(listKey) ?? []
    const index = prev.findIndex((x) => x.id === m.id)
    if (index < 0) return

    qc.setQueryData<MessageDto[]>(listKey, (old) => (old ?? []).filter((x) => x.id !== m.id))
    qc.invalidateQueries({ queryKey: ["conversations"] })

    const startedAtMs = Date.now()
    const timeoutId = window.setTimeout(async () => {
      try {
        await api.delete(`/api/messages/${m.id}`)
      } catch {
        qc.invalidateQueries({ queryKey: ["messages", m.conversationId] })
        qc.invalidateQueries({ queryKey: ["conversations"] })
      } finally {
        setPendingDelete(null)
      }
    }, 5000)

    setPendingDelete({ message: m, index, startedAtMs, timeoutId })
    setUndoLeftMs(5000)
  }

  const undoDelete = () => {
    if (!pendingDelete) return
    window.clearTimeout(pendingDelete.timeoutId)

    const m = pendingDelete.message
    const listKey = ["messages", m.conversationId] as const

    qc.setQueryData<MessageDto[]>(listKey, (old) => {
      const arr = [...(old ?? [])]
      const idx = Math.min(Math.max(0, pendingDelete.index), arr.length)
      arr.splice(idx, 0, m)
      return arr
    })

    qc.invalidateQueries({ queryKey: ["conversations"] })
    setPendingDelete(null)
  }

  const onDragEnter = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    dragDepthRef.current++
    if (!activeConversationId) return
    setDragging(true)
  }

  const onDragLeave = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    dragDepthRef.current = Math.max(0, dragDepthRef.current - 1)
    if (dragDepthRef.current === 0) setDragging(false)
  }

  const onDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
  }

  const onDrop = async (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    dragDepthRef.current = 0
    setDragging(false)
    if (!activeConversationId) return
    await uploadFiles(e.dataTransfer.files)
  }

  const currentImageItems = useMemo<LightboxItem[]>(() => {
    const msgs = msgsQ.data ?? []
    const flat: LightboxItem[] = []
    const seen = new Set<string>()
    for (const m of msgs) {
      for (const a of m.attachments ?? []) {
        if (!(a.contentType ?? "").toLowerCase().startsWith("image/")) continue
        if (seen.has(a.id)) continue
        seen.add(a.id)
        flat.push({ id: a.id, fileName: a.fileName })
      }
    }
    return flat
  }, [msgsQ.data])

  const openLightboxAt = async (attachmentId: string) => {
    const items = currentImageItems
    const idx = items.findIndex((x) => x.id === attachmentId)
    if (idx < 0) return
    const item = items[idx]
    await ensureImagePreview(item.id, item.fileName)
    setLightbox({ items, index: idx })
  }

  const closeLightbox = () => setLightbox(null)

  const canPrev = !!lightbox && lightbox.index > 0
  const canNext = !!lightbox && lightbox.index < lightbox.items.length - 1

  const goPrev = async () => {
    if (!lightbox || lightbox.index <= 0) return
    const nextIndex = lightbox.index - 1
    const item = lightbox.items[nextIndex]
    await ensureImagePreview(item.id, item.fileName)
    setLightbox({ items: lightbox.items, index: nextIndex })
  }

  const goNext = async () => {
    if (!lightbox || lightbox.index >= lightbox.items.length - 1) return
    const nextIndex = lightbox.index + 1
    const item = lightbox.items[nextIndex]
    await ensureImagePreview(item.id, item.fileName)
    setLightbox({ items: lightbox.items, index: nextIndex })
  }

  useEffect(() => {
    if (!lightbox) return
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeLightbox()
      if (e.key === "ArrowLeft") goPrev()
      if (e.key === "ArrowRight") goNext()
    }
    window.addEventListener("keydown", onKeyDown)
    return () => window.removeEventListener("keydown", onKeyDown)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lightbox])

  return (
    <div className="h-screen w-screen bg-background">
      {/* modals (opened from burger) */}
      <NewDirectDialog
        open={directOpen}
        onOpenChange={setDirectOpen}
        hideTrigger
        onCreated={(id) => setActiveConversationId(id)}
      />
      <NewGroupDialog
        open={groupOpen}
        onOpenChange={setGroupOpen}
        hideTrigger
        onCreated={(id) => setActiveConversationId(id)}
      />
      <AllUsersDialog
        meId={meId}
        open={usersOpen}
        onOpenChange={setUsersOpen}
        hideTrigger
        onOpenConversation={(id) => setActiveConversationId(id)}
      />

      <div className="h-full flex">
        <div className="w-[360px] border-r bg-muted/20 flex flex-col">
          <div className="p-3 flex items-center gap-2">
            <Input value={sidebarQuery} onChange={(e) => setSidebarQuery(e.target.value)} placeholder="Search" className="bg-background" />
          </div>

          <div className="px-3 pb-3 flex items-center justify-between gap-2">
            <div className="text-sm text-muted-foreground truncate">{user?.userName ?? ""}</div>

            <div className="flex items-center gap-2">
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="icon" className="h-9 w-9" aria-label="Menu">
                    <Menu className="h-4 w-4" />
                  </Button>
                </DropdownMenuTrigger>

                <DropdownMenuContent align="end" className="w-56">
                  <DropdownMenuItem onClick={() => setDirectOpen(true)}>Новый чат</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setGroupOpen(true)}>Новая группа</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setUsersOpen(true)}>Пользователи</DropdownMenuItem>
                  <DropdownMenuItem className="text-destructive focus:text-destructive" onClick={logout}>
                    Выйти
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>

          <Separator />

          <ScrollArea className="flex-1">
            <div className="p-2 space-y-1">
              {convs.map((c) => (
                <button
                  key={c.id}
                  className={cn(
                    "w-full text-left rounded-xl px-3 py-2 hover:bg-muted transition",
                    activeConversationId === c.id ? "bg-muted" : ""
                  )}
                  onClick={() => setActiveConversationId(c.id)}
                >
                  <div className="flex items-center justify-between gap-2">
                    <div className="truncate font-medium">
                      <ConversationTitle meId={meId} c={c} />
                    </div>
                    {c.unreadCount > 0 && (
                      <div className="text-xs px-2 py-0.5 rounded-full bg-primary text-primary-foreground">
                        {c.unreadCount}
                      </div>
                    )}
                  </div>

                  <div className={cn("text-xs truncate mt-1", hasTyping(c.id) ? "text-primary" : "text-muted-foreground")}>
                    {hasTyping(c.id) ? <TypingText conversationId={c.id} meId={meId} /> : c.lastMessageText ?? ""}
                  </div>
                </button>
              ))}
              {convsAll.length === 0 && <div className="text-sm text-muted-foreground p-3">Пока нет чатов</div>}
            </div>
          </ScrollArea>
        </div>

        <div className="flex-1 flex flex-col min-w-0">
          <div className="h-14 border-b flex items-center justify-between px-4 bg-background/80 backdrop-blur">
            <div className="min-w-0">
              <div className="font-medium truncate">
                {active ? <ConversationTitle meId={meId} c={active} /> : "Выбери чат"}
              </div>
              <div className="text-xs text-muted-foreground">
                {active ? (hasTyping(active.id) ? <TypingText conversationId={active.id} meId={meId} /> : "") : ""}
              </div>
            </div>
          </div>

          <div
            className="flex-1 min-h-0 relative"
            onDragEnter={onDragEnter}
            onDragLeave={onDragLeave}
            onDragOver={onDragOver}
            onDrop={onDrop}
          >
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,hsl(var(--muted))_0%,transparent_35%),radial-gradient(circle_at_80%_30%,hsl(var(--muted))_0%,transparent_40%),radial-gradient(circle_at_30%_80%,hsl(var(--muted))_0%,transparent_40%)] opacity-40 pointer-events-none" />

            {dragging && (
              <div className="absolute inset-0 z-40 flex items-center justify-center bg-black/30">
                <div className="rounded-2xl border bg-background px-6 py-5 shadow-lg flex items-center gap-3">
                  <UploadCloud className="h-6 w-6" />
                  <div>
                    <div className="font-medium">Отпусти файлы, чтобы прикрепить</div>
                    <div className="text-xs text-muted-foreground">Загрузка начнётся сразу</div>
                  </div>
                </div>
              </div>
            )}

            <ScrollArea className="h-full">
              <div className="px-4 py-4 space-y-2">
                {(msgsQ.data ?? []).map((m) => (
                  <MessageBubble
                    key={m.id}
                    meId={meId}
                    m={m}
                    onReply={(x) => setReplyTo(x)}
                    onJumpTo={(id) => jumpTo(id)}
                    onDelete={(x) => scheduleDeleteWithUndo(x)}
                    onDownloadAttachment={downloadAttachment}
                    previewUrlByAttachmentId={previewUrlByAttachmentId}
                    ensureImagePreview={ensureImagePreview}
                    onOpenImage={(attachmentId) => openLightboxAt(attachmentId)}
                  />
                ))}

                {(msgsQ.data ?? []).length === 0 && activeConversationId && (
                  <div className="text-sm text-muted-foreground text-center py-10">Нет сообщений</div>
                )}
                {!activeConversationId && (
                  <div className="text-sm text-muted-foreground text-center py-10">Выбери чат слева или создай новый</div>
                )}
              </div>
            </ScrollArea>

            {pendingDelete && (
              <div className="absolute left-0 right-0 bottom-3 flex justify-center px-3 z-10">
                <div className="w-full max-w-xl rounded-2xl border bg-background/95 backdrop-blur shadow-lg px-3 py-2 flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <div className="text-sm font-medium truncate">Сообщение удалится через {Math.ceil(undoLeftMs / 1000)} сек.</div>
                    <div className="text-xs text-muted-foreground truncate">{pendingDelete.message.text ? pendingDelete.message.text : "Без текста"}</div>
                  </div>
                  <Button variant="outline" size="sm" onClick={undoDelete}>
                    <Undo2 className="h-4 w-4 mr-2" />
                    Undo
                  </Button>
                </div>
              </div>
            )}

            {lightbox && (
              <div className="absolute inset-0 z-50 bg-black/70 flex items-center justify-center p-4" onClick={closeLightbox}>
                <div className="w-full max-w-6xl bg-background rounded-2xl border shadow-xl overflow-hidden" onClick={(e) => e.stopPropagation()}>
                  <div className="p-3 border-b flex items-center justify-between gap-2">
                    <div className="min-w-0 flex items-center gap-2">
                      <ImageIcon className="h-4 w-4 shrink-0" />
                      <div className="text-sm font-medium truncate">{lightbox.items[lightbox.index]?.fileName ?? ""}</div>
                      <div className="text-xs text-muted-foreground shrink-0">
                        {lightbox.index + 1}/{lightbox.items.length}
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => downloadAttachment(lightbox.items[lightbox.index].id, lightbox.items[lightbox.index].fileName)}
                      >
                        <Download className="h-4 w-4 mr-2" />
                        Скачать
                      </Button>
                      <Button variant="outline" size="sm" onClick={closeLightbox}>
                        Закрыть
                      </Button>
                    </div>
                  </div>

                  <div className="relative p-3 bg-black/5 flex items-center justify-center">
                    <Button
                      variant="ghost"
                      size="icon"
                      className={cn("absolute left-2 top-1/2 -translate-y-1/2 h-10 w-10 rounded-full", !canPrev ? "opacity-30 pointer-events-none" : "")}
                      onClick={goPrev}
                      aria-label="Previous"
                      title="←"
                    >
                      <ChevronLeft className="h-6 w-6" />
                    </Button>

                    {(() => {
                      const item = lightbox.items[lightbox.index]
                      const src = item ? previewUrlByAttachmentId[item.id] : undefined
                      return src ? (
                        <img src={src} alt={item.fileName} className="max-h-[78vh] w-auto object-contain rounded-xl" />
                      ) : (
                        <div className="py-12 text-sm text-muted-foreground">Загрузка…</div>
                      )
                    })()}

                    <Button
                      variant="ghost"
                      size="icon"
                      className={cn("absolute right-2 top-1/2 -translate-y-1/2 h-10 w-10 rounded-full", !canNext ? "opacity-30 pointer-events-none" : "")}
                      onClick={goNext}
                      aria-label="Next"
                      title="→"
                    >
                      <ChevronRight className="h-6 w-6" />
                    </Button>
                  </div>

                  <div className="px-3 pb-3 text-xs text-muted-foreground">←/→ — листать • Esc — закрыть</div>
                </div>
              </div>
            )}
          </div>

          <div className="p-3 border-t bg-background space-y-2">
            {replyTo && (
              <div className="flex items-center justify-between gap-2 rounded-xl border px-3 py-2">
                <div className="min-w-0">
                  <div className="text-xs font-medium truncate">Ответ: {replyTo.senderUserName}</div>
                  <div className="text-xs text-muted-foreground truncate">{replyTo.text}</div>
                </div>
                <Button variant="outline" size="sm" onClick={() => setReplyTo(null)}>
                  X
                </Button>
              </div>
            )}

            {attachments.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {attachments.map((a) => {
                  const isImg = (a.contentType ?? "").toLowerCase().startsWith("image/")
                  const thumb = isImg ? previewUrlByAttachmentId[a.id] : undefined

                  return (
                    <div key={a.id} className="flex items-center gap-2 rounded-xl border px-3 py-1.5">
                      {isImg && thumb && (
                        <button
                          type="button"
                          className="shrink-0 rounded-lg overflow-hidden border"
                          onClick={() => openLightboxAt(a.id)}
                          title="Открыть"
                        >
                          <img src={thumb} alt={a.fileName} className="h-10 w-10 object-cover" />
                        </button>
                      )}

                      <div className="min-w-0">
                        <div className="text-xs font-medium truncate max-w-[260px]">{a.fileName}</div>
                        <div className="text-[11px] text-muted-foreground truncate max-w-[260px]">
                          {a.contentType} • {formatBytes(Number(a.size))}
                        </div>
                      </div>

                      <Button variant="ghost" size="icon" className="h-7 w-7 rounded-full" onClick={() => removeAttachment(a.id)}>
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  )
                })}
              </div>
            )}

            {uploadErr && <div className="text-sm text-red-600">{uploadErr}</div>}

            <div className="flex items-end gap-2">
              <input ref={fileInputRef} type="file" multiple className="hidden" onChange={(e) => uploadFiles(e.target.files)} />

              <Button
                type="button"
                variant="outline"
                className="h-11"
                onClick={() => fileInputRef.current?.click()}
                disabled={!activeConversationId || uploading || sending}
                title="Прикрепить файл"
              >
                <Paperclip className="h-4 w-4 mr-2" />
                {uploading ? "Загрузка..." : "Файл"}
              </Button>

              <Input
                value={text}
                onChange={(e) => {
                  setText(e.target.value)
                  typingControllerRef.current?.onType()
                }}
                onBlur={() => typingControllerRef.current?.onStop()}
                placeholder={activeConversationId ? "Message" : "Выбери чат"}
                disabled={!activeConversationId || sending}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault()
                    send()
                  }
                }}
                className="h-11"
              />

              <Button
                onClick={send}
                disabled={!activeConversationId || sending || uploading || (text.trim().length === 0 && attachments.length === 0)}
                className="h-11"
              >
                Send
              </Button>
            </div>

            <div className="text-[11px] text-muted-foreground">Enter — отправить • Можно перетащить файлы мышкой</div>
          </div>
        </div>
      </div>
    </div>
  )
}
