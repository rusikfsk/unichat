export type ConversationType = 1 | 2 | 3

export type ConversationListItemDto = {
  id: string
  type: ConversationType
  title: string
  ownerId: string | null
  createdAt: string
  unreadCount: number
  lastMessageText: string | null
  lastMessageAt: string | null
}

export type AttachmentDto = {
  id: string
  fileName: string
  contentType: string
  size: number
  createdAt: string
}

export type ReplyPreviewDto = {
  id: string
  senderId: string
  senderUserName: string
  text: string
  createdAt: string
}

export type MessageDto = {
  id: string
  conversationId: string
  senderId: string
  senderUserName: string
  text: string
  createdAt: string
  replyToMessageId: string | null
  replyTo: ReplyPreviewDto | null
  attachments: AttachmentDto[]
}

export type MemberDto = {
  userId: string
  userName: string
  role: number
  permissions: number
  joinedAt: string
  lastReadAt: string | null
}

export type ConversationDetailsDto = {
  id: string
  type: ConversationType
  title: string
  ownerId: string | null
  createdAt: string
  members: MemberDto[]
}

export type ConversationDto = {
  id: string
  type: ConversationType
  title: string
  ownerId: string | null
  createdAt: string
}

export type UserSearchItemDto = {
  id: string
  userName: string
}

export type UserDto = {
  id: string
  userName: string
  createdAt: string
}
