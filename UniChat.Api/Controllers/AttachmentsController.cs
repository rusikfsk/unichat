using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Attachments;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public sealed class AttachmentsController : ControllerBase
{
    private readonly UniChatDbContext _db;
    private readonly IWebHostEnvironment _env;

    private const long MaxImageBytes = 10L * 1024 * 1024;   // 10 MB
    private const long MaxVideoBytes = 200L * 1024 * 1024;  // 200 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg","image/png","image/webp","image/gif",
        "video/mp4","video/webm","video/quicktime","video/x-matroska",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",".jpeg",".png",".webp",".gif",
        ".mp4",".webm",".mov",".mkv"
    };

    public AttachmentsController(UniChatDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // POST /api/attachments
    [HttpPost]
    [RequestSizeLimit(MaxVideoBytes)]
    public async Task<ActionResult<AttachmentDto>> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        var contentType = file.ContentType ?? "application/octet-stream";
        var originalName = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(originalName);

        if (!AllowedContentTypes.Contains(contentType) || !AllowedExtensions.Contains(ext))
            return BadRequest("File type is not allowed.");

        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        var max = isImage ? MaxImageBytes : (isVideo ? MaxVideoBytes : 0);

        if (max == 0) return BadRequest("File type is not allowed.");
        if (file.Length > max)
            return BadRequest($"File is too large. Max allowed: {max / (1024 * 1024)} MB.");

        var me = User.GetUserId();

        var storageRoot = Path.Combine(_env.ContentRootPath, "Storage");
        Directory.CreateDirectory(storageRoot);

        var id = Guid.NewGuid();
        var storedFileName = $"{id}{ext}";
        var fullPath = Path.Combine(storageRoot, storedFileName);

        await using (var fs = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
        }

        var entity = new UniChat.Domain.Entities.Attachment
        {
            Id = id,
            UploaderId = me,
            MessageId = null,
            FileName = originalName,
            ContentType = contentType,
            Size = file.Length,
            StoragePath = fullPath,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Attachments.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new AttachmentDto(entity.Id, entity.FileName, entity.ContentType, entity.Size, entity.CreatedAt));
    }

    // GET /api/attachments/{id}
    // ✅ Авторизация: участник чата, где сообщение, ИЛИ uploader (если не привязан)
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var me = User.GetUserId();

        var att = await _db.Attachments
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.UploaderId,
                a.MessageId,
                a.FileName,
                a.ContentType,
                a.StoragePath
            })
            .SingleOrDefaultAsync();

        if (att == null) return NotFound();

        // 1) если файл ещё не привязан к сообщению — скачать может только uploader
        if (att.MessageId == null)
        {
            if (att.UploaderId != me) return Forbid();
        }
        else
        {
            // 2) привязан: проверяем, что пользователь участник разговора
            var conversationId = await _db.Messages
                .Where(m => m.Id == att.MessageId.Value)
                .Select(m => (Guid?)m.ConversationId)
                .SingleOrDefaultAsync();

            if (conversationId == null) return NotFound();

            var isMember = await _db.Memberships
                .AnyAsync(ms => ms.ConversationId == conversationId.Value && ms.UserId == me);

            if (!isMember) return Forbid();
        }

        if (!System.IO.File.Exists(att.StoragePath))
            return NotFound("File missing on disk.");

        return PhysicalFile(att.StoragePath, att.ContentType, att.FileName, enableRangeProcessing: true);
    }
}
