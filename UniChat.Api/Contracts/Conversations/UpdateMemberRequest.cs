using UniChat.Domain.Entities;

namespace UniChat.Api.Contracts.Conversations;

public record UpdateMemberRequest(
    MemberRole Role,
    ChannelPermissions Permissions
);
