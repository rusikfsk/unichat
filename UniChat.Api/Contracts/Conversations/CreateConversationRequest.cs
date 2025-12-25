using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record CreateConversationRequest(
    ConversationType Type,
    string Title,
    List<Guid>? MemberIds
);
