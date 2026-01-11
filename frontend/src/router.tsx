import { createBrowserRouter } from "react-router-dom"
import { RootLayout } from "@/ui/layouts/RootLayout"
import { LoginPage } from "@/ui/pages/LoginPage"
import { RegisterPage } from "@/ui/pages/RegisterPage"
import { ConfirmEmailPage } from "@/ui/pages/ConfirmEmailPage"
import { ChatsPage } from "@/ui/pages/ChatsPage"

export const router = createBrowserRouter([
  {
    path: "/",
    element: <RootLayout />,
    children: [
      { path: "login", element: <LoginPage /> },
      { path: "register", element: <RegisterPage /> },
      { path: "confirm-email", element: <ConfirmEmailPage /> },
      { path: "chats", element: <ChatsPage /> }
    ]
  }
])
