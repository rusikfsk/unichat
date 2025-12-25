namespace UniChat.Domain.Entities;

public enum ConversationType
{
    Direct = 1,
    Group = 2,
    Channel = 3
}

public class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; }
    public string Title { get; set; } = default!;
    public Guid? OwnerId { get; set; } // владелец для каналов
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Membership> Members { get; set; } = new();
}
