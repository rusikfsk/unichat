namespace UniChat.Domain.Entities;

[Flags]
public enum ChannelPermissions
{
    None = 0,
    Write = 1,
    Invite = 2,
    ManageRoles = 4,
    DeleteMessages = 8,
    ManageChannel = 16,
}

public enum MemberRole
{
    Member = 1,
    Admin = 2,
    Owner = 3
}

public class Membership
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }

    public MemberRole Role { get; set; } = MemberRole.Member;
    public ChannelPermissions Permissions { get; set; } = ChannelPermissions.Write;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReadAt { get; set; }

}
