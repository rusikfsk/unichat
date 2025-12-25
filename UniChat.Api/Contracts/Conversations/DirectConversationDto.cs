namespace UniChat.Api.Contracts.Conversations;

public record DirectConversationDto(
    Guid ConversationId,
    Guid PeerUserId,
    string PeerUserName,
    DateTimeOffset CreatedAt
);
