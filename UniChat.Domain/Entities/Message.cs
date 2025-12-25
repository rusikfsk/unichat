using System.Net.Mail;

namespace UniChat.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }

    public string? Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ReplyToMessageId { get; set; }
    public DateTimeOffset? EditedAt { get; set; }

    public List<Attachment> Attachments { get; set; } = new();
}
