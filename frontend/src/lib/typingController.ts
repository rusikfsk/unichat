import type { HubConnection } from "@microsoft/signalr"

export class TypingController {
  private readonly conn: HubConnection
  private readonly getConversationId: () => string | null
  private typingSentAt = 0
  private stopTimer: number | null = null

  constructor(conn: HubConnection, getConversationId: () => string | null) {
    this.conn = conn
    this.getConversationId = getConversationId
  }

  onType() {
    const conversationId = this.getConversationId()
    if (!conversationId) return
    if (this.conn.state !== "Connected") return

    const now = Date.now()
    if (now - this.typingSentAt > 700) {
      this.typingSentAt = now
      this.conn.invoke("Typing", conversationId).catch(() => {})
    }

    if (this.stopTimer) window.clearTimeout(this.stopTimer)
    this.stopTimer = window.setTimeout(() => {
      this.stopTimer = null
      this.onStop()
    }, 1200)
  }

  onStop() {
    const conversationId = this.getConversationId()
    if (!conversationId) return
    if (this.conn.state !== "Connected") return
    this.conn.invoke("StopTyping", conversationId).catch(() => {})
  }

  dispose() {
    if (this.stopTimer) window.clearTimeout(this.stopTimer)
    this.stopTimer = null
  }
}
