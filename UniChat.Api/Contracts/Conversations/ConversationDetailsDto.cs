using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record ConversationDetailsDto(
    Guid Id,
    ConversationType Type,
    string Title,
    Guid? OwnerId,
    DateTimeOffset CreatedAt,
    List<MemberDto> Members
);

