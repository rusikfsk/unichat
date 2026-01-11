namespace UniChat.Api.Contracts.Messages;

public record CreateMessageRequest(
    Guid ConversationId,
    string Text,
    List<Guid>? AttachmentIds,
    Guid? ReplyToMessageId
);
