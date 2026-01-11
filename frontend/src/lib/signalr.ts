import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { env } from "@/lib/env"
import { getTokens } from "@/lib/tokens"

let conn: ReturnType<HubConnectionBuilder["build"]> | null = null
let starting: Promise<void> | null = null

export function getHub() {
  if (conn) return conn

  conn = new HubConnectionBuilder()
    .withUrl(`${env.apiUrl}/hubs/chat`, {
      accessTokenFactory: () => getTokens()?.accessToken ?? ""
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build()

  return conn
}

export async function ensureHubConnected() {
  const token = getTokens()?.accessToken
  if (!token) return

  const hub = getHub()

  if (hub.state === HubConnectionState.Connected) return

  if (starting) {
    await starting
    return
  }

  starting = (async () => {
    while (hub.state !== HubConnectionState.Connected) {
      if (hub.state === HubConnectionState.Disconnected) {
        await hub.start()
        break
      }
      await new Promise((r) => setTimeout(r, 50))
    }
  })()

  try {
    await starting
  } finally {
    starting = null
  }
}
