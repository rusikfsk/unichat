namespace UniChat.Domain.Entities;

public enum AttachmentType
{
    Image = 1,
    Video = 2,
    File = 3
}

public sealed class Attachment
{
    public Guid Id { get; set; }
    public Guid UploaderId { get; set; }

    public Guid? MessageId { get; set; }

    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long Size { get; set; }

    public string StoragePath { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
