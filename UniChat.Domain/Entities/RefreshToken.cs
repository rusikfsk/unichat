namespace UniChat.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // Храним НЕ токен, а хеш
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
