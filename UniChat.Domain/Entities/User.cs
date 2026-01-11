namespace UniChat.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    
    public string UserName { get; set; } = default!;

    
    public string DisplayName { get; set; } = default!;

    
    public string Email { get; set; } = default!;

    public DateTimeOffset? EmailConfirmedAt { get; set; }

    
    public string? EmailConfirmationTokenHash { get; set; }

    
    public Guid? AvatarAttachmentId { get; set; }

    public string PasswordHash { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
