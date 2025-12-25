using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record MemberDto(
    Guid UserId,
    string UserName,
    MemberRole Role,
    ChannelPermissions Permissions,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LastReadAt
);
