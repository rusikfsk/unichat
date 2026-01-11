import { useQuery } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { ConversationDetailsDto, ConversationListItemDto } from "@/types/api"
import { usePresenceStore } from "@/stores/presenceStore"
import { relativeTimeRu } from "@/lib/relativeTimeRu"
import { OnlineDot } from "@/ui/components/OnlineDot"

export function ConversationTitle(props: { meId: string; c: ConversationListItemDto }) {
  const isDirect = props.c.type === 1
  const onlineIds = usePresenceStore((s) => s.onlineUserIds)
  const lastSeenByUserId = usePresenceStore((s) => s.lastSeenByUserId)

  const detailsQ = useQuery({
    queryKey: ["conversation.details", props.c.id],
    enabled: isDirect && !!props.meId,
    staleTime: 60_000,
    retry: false,
    queryFn: async () => {
      const res = await api.get<ConversationDetailsDto>(`/api/conversations/${props.c.id}`)
      return res.data
    }
  })

  if (!isDirect) return <>{props.c.title}</>

  const members = detailsQ.data?.members ?? []
  const peer = members.find((m) => m.userId !== props.meId)
  const peerId = peer?.userId ?? ""
  const peerName = peer?.userName ?? "Direct"

  const online = peerId ? onlineIds.has(peerId) : false
  const lastSeenAt = peerId ? lastSeenByUserId[peerId] ?? null : null

  if (online) {
    return (
      <span className="flex items-center gap-2 min-w-0">
        <OnlineDot online={true} />
        <span className="truncate">{peerName}</span>
      </span>
    )
  }

  const status = lastSeenAt ? `был(а) ${relativeTimeRu(lastSeenAt)}` : "не в сети"

  return (
    <span className="flex items-center gap-2 min-w-0">
      <span className="truncate">{peerName}</span>
      <span className="text-xs text-muted-foreground truncate">{status}</span>
    </span>
  )
}
