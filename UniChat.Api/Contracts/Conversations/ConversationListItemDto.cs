using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record ConversationListItemDto(
    Guid Id,
    ConversationType Type,
    string Title,
    Guid? OwnerId,
    DateTimeOffset CreatedAt,
    int UnreadCount,
    string? LastMessageText,
    DateTimeOffset? LastMessageAt
);
