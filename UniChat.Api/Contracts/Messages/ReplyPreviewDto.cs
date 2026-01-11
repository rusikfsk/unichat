namespace UniChat.Api.Contracts.Messages;

public record ReplyPreviewDto(
    Guid Id,
    Guid SenderId,
    string SenderUserName,
    string Text,
    DateTimeOffset CreatedAt
);
