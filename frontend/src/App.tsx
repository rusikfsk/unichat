import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { LoginPage } from './pages/LoginPage'
import { ChatsPage } from './pages/ChatsPage'
import { getTokens } from './api/http'

function RequireAuth({ children }: { children: JSX.Element }) {
  const t = getTokens()
  if (!t?.accessToken) return <Navigate to="/" replace />
  return children
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LoginPage />} />
        <Route
          path="/chats"
          element={
            <RequireAuth>
              <ChatsPage />
            </RequireAuth>
          }
        />
      </Routes>
    </BrowserRouter>
  )
}
