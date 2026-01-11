import { useEffect, useMemo } from "react"
import type { MessageDto } from "@/types/api"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@/components/ui/dropdown-menu"
import { MoreHorizontal, Reply, Trash2, Download } from "lucide-react"

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

export function MessageBubble(props: {
  meId: string
  m: MessageDto
  onReply: (m: MessageDto) => void
  onJumpTo: (messageId: string) => void
  onDelete: (m: MessageDto) => void

  onDownloadAttachment: (attachmentId: string, fileName: string) => void

  // image preview + lightbox
  previewUrlByAttachmentId: Record<string, string | undefined>
  ensureImagePreview: (attachmentId: string, fileName: string) => void
  onOpenImage: (attachmentId: string) => void
}) {
  const mine = props.m.senderId === props.meId
  const atts = props.m.attachments ?? []

  const imageAtts = useMemo(
    () => atts.filter((a) => (a.contentType ?? "").toLowerCase().startsWith("image/")),
    [atts]
  )

  useEffect(() => {
    for (const a of imageAtts) {
      if (!props.previewUrlByAttachmentId[a.id]) {
        props.ensureImagePreview(a.id, a.fileName)
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageAtts])

  return (
    <div id={`msg-${props.m.id}`} className={cn("flex w-full group", mine ? "justify-end" : "justify-start")}>
      <div className="max-w-[72%] relative">
        {/* ⋯ menu */}
        <div className={cn("absolute -top-2", mine ? "-left-10" : "-right-10")}>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 rounded-full opacity-0 group-hover:opacity-100 transition"
                aria-label="Message actions"
              >
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>

            <DropdownMenuContent align={mine ? "start" : "end"} className="w-44">
              <DropdownMenuItem onClick={() => props.onReply(props.m)}>
                <Reply className="h-4 w-4 mr-2" />
                Reply
              </DropdownMenuItem>

              {mine && (
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onClick={() => props.onDelete(props.m)}
                >
                  <Trash2 className="h-4 w-4 mr-2" />
                  Delete
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <div
          className={cn(
            "rounded-2xl px-3 py-2 text-sm shadow-sm",
            mine ? "bg-primary text-primary-foreground rounded-br-md" : "bg-card text-foreground rounded-bl-md border"
          )}
        >
          {props.m.replyTo && (
            <button
              className={cn("w-full text-left rounded-xl px-2 py-1 mb-2", mine ? "bg-primary-foreground/15" : "bg-muted")}
              onClick={() => props.onJumpTo(props.m.replyTo!.id)}
            >
              <div className={cn("text-[11px] font-medium", mine ? "text-primary-foreground" : "text-foreground")}>
                {props.m.replyTo.senderUserName}
              </div>
              <div className={cn("text-[11px] opacity-80 truncate", mine ? "text-primary-foreground" : "text-foreground")}>
                {props.m.replyTo.text}
              </div>
            </button>
          )}

          {!mine && <div className="text-[11px] opacity-70 mb-1">{props.m.senderUserName}</div>}

          {props.m.text && <div className="whitespace-pre-wrap break-words">{props.m.text}</div>}

          {/* image previews */}
          {imageAtts.length > 0 && (
            <div className="mt-2 grid grid-cols-2 gap-2">
              {imageAtts.map((a) => {
                const src = props.previewUrlByAttachmentId[a.id]
                return (
                  <button
                    key={a.id}
                    type="button"
                    className={cn("rounded-xl overflow-hidden border", mine ? "border-primary-foreground/20" : "border-border")}
                    onClick={() => props.onOpenImage(a.id)}
                    title="Открыть"
                  >
                    {src ? (
                      <img src={src} alt={a.fileName} className="w-full h-40 object-cover" />
                    ) : (
                      <div className="w-full h-40 flex items-center justify-center text-xs opacity-70">Загрузка…</div>
                    )}
                  </button>
                )
              })}
            </div>
          )}

          {/* attachments list */}
          {atts.length > 0 && (
            <div className="mt-2 space-y-1">
              {atts.map((a) => (
                <div
                  key={a.id}
                  className={cn(
                    "flex items-center justify-between gap-2 rounded-xl px-2 py-1",
                    mine ? "bg-primary-foreground/15" : "bg-muted"
                  )}
                >
                  <div className="min-w-0">
                    <div className={cn("text-[12px] truncate", mine ? "text-primary-foreground" : "text-foreground")}>
                      {a.fileName}
                    </div>
                    <div className={cn("text-[11px] opacity-70", mine ? "text-primary-foreground" : "text-foreground")}>
                      {a.contentType} • {formatBytes(Number(a.size))}
                    </div>
                  </div>

                  <Button
                    variant="ghost"
                    size="icon"
                    className={cn("h-8 w-8 rounded-full", mine ? "hover:bg-primary-foreground/20" : "hover:bg-background/50")}
                    onClick={() => props.onDownloadAttachment(a.id, a.fileName)}
                    title="Скачать"
                  >
                    <Download className={cn("h-4 w-4", mine ? "text-primary-foreground" : "text-foreground")} />
                  </Button>
                </div>
              ))}
            </div>
          )}

          <div className="mt-2 flex items-center justify-end">
            <div className={cn("text-[10px] opacity-70", mine ? "text-right" : "text-left")}>
              {new Date(props.m.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
