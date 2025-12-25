using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record ConversationDto(
    Guid Id,
    ConversationType Type,
    string Title,
    Guid? OwnerId,
    DateTimeOffset CreatedAt
);
