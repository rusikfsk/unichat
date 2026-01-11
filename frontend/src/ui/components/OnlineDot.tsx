import { cn } from "@/lib/utils"

export function OnlineDot(props: { online: boolean; size?: number }) {
  const size = props.size ?? 10

  return (
    <span
      className={cn("inline-block rounded-full shrink-0", props.online ? "bg-green-500" : "bg-red-500")}
      style={{ width: size, height: size }}
    />
  )
}
