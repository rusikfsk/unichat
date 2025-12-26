import { Outlet } from 'react-router-dom'
import styles from './ChatsLayout.module.css'

export default function ChatsLayout() {
  return (
    <div className={styles.app}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarTop}>
          <button className={styles.menuBtn} aria-label="Menu">
            ☰
          </button>
          <input className={styles.search} placeholder="Search" />
        </div>

        <div className={styles.chatList}>
          <div className={styles.chatStub}>No chats yet</div>
        </div>
      </aside>

      <main className={styles.content}>
        <Outlet />
      </main>
    </div>
  )
}