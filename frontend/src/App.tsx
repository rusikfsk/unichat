import React from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import LoginPage from './pages/LoginPage'
import ChatsLayout from './layouts/ChatsLayout'
import { getTokens } from './api/http'

function RequireAuth(props: { children: React.ReactNode }) {
  const t = getTokens()
  if (!t?.accessToken) return <Navigate to="/login" replace />
  return <>{props.children}</>
}

function ChatEmpty() {
  return <div style={{ opacity: 0.7 }}>Select a chat</div>
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* ✅ логин — БЕЗ layout */}
        <Route path="/login" element={<LoginPage />} />

        {/* ✅ чаты — ВНУТРИ layout */}
        <Route
          path="/chats"
          element={
            <RequireAuth>
              <ChatsLayout />
            </RequireAuth>
          }
        >
          <Route index element={<ChatEmpty />} />
          {/* позже: <Route path=":id" element={<ChatPage />} /> */}
        </Route>

        {/* редиректы */}
        <Route path="/" element={<Navigate to="/chats" replace />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
