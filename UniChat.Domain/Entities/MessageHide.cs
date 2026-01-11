namespace UniChat.Domain.Entities;

public sealed class MessageHide
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset HiddenAt { get; set; } = DateTimeOffset.UtcNow;
}

