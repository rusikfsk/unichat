import { Outlet, Navigate, useLocation } from "react-router-dom"
import { useEffect } from "react"
import { useAuthStore } from "@/stores/auth"

export function RootLayout() {
  const tokens = useAuthStore((s) => s.tokens)
  const fetchMe = useAuthStore((s) => s.fetchMe)
  const user = useAuthStore((s) => s.user)
  const location = useLocation()

  useEffect(() => {
    fetchMe()
  }, [fetchMe, tokens?.accessToken])

  const isAuthRoute =
    location.pathname.startsWith("/login") ||
    location.pathname.startsWith("/register") ||
    location.pathname.startsWith("/confirm-email")

  if (!tokens?.accessToken && !isAuthRoute) return <Navigate to="/login" replace />
  if (tokens?.accessToken && !user && isAuthRoute) return <Navigate to="/chats" replace />

  return <Outlet />
}
