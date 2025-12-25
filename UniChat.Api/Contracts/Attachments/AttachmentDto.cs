namespace UniChat.Api.Contracts.Attachments;

public record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt
);
