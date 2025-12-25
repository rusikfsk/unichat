using UniChat.Api.Contracts.Attachments;

namespace UniChat.Api.Contracts.Messages;

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderUserName,
    string Text,
    DateTimeOffset CreatedAt,
    List<AttachmentDto> Attachments
);
