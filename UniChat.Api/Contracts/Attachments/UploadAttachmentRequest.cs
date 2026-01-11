using Microsoft.AspNetCore.Http;

namespace UniChat.Api.Contracts.Attachments;

public sealed class UploadAttachmentRequest
{
    public IFormFile File { get; set; } = default!;
}
