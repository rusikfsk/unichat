import { useMemo } from "react"
import { useQuery } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { UserDto } from "@/types/api"
import { useTypingStore } from "@/stores/typingStore"

export function TypingText(props: { conversationId: string; meId: string }) {
  const ids = useTypingStore((s) => s.byConversationId[props.conversationId] ?? [])

  const others = useMemo(() => ids.filter((x) => x && x !== props.meId), [ids, props.meId])

  const firstId = others[0] ?? null
  const count = others.length

  const userQ = useQuery({
    queryKey: ["user", firstId],
    enabled: !!firstId,
    staleTime: 5 * 60_000,
    retry: false,
    queryFn: async () => {
      const res = await api.get<UserDto>(`/api/users/${firstId}`)
      return res.data
    }
  })

  if (count === 0) return null
  if (count >= 2) return <>{count} печатают…</>

  const name = userQ.data?.userName
  return <>{name ? `${name} печатает…` : "печатает…"}</>
}
