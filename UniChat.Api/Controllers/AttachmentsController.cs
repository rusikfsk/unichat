using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Attachments;
using UniChat.Api.Services;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public sealed class AttachmentsController : ControllerBase
{
    private readonly UniChatDbContext _db;
    private readonly IFileStorage _storage;

    private const long MaxImageBytes = 10L * 1024 * 1024;   // 10 MB
    private const long MaxVideoBytes = 200L * 1024 * 1024;  // 200 MB

    public AttachmentsController(UniChatDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // POST /api/attachments
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxVideoBytes)]
    public async Task<ActionResult<AttachmentDto>> Upload([FromForm] UploadAttachmentRequest req, CancellationToken ct)
    {
        var file = req.File;

        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        if (file.Length > MaxVideoBytes)
            return BadRequest($"File is too large. Max allowed: {MaxVideoBytes / (1024 * 1024)} MB.");

        var originalName = Path.GetFileName(file.FileName ?? "file");
        if (string.IsNullOrWhiteSpace(originalName))
            originalName = "file";

        var me = User.GetUserId();

        var sniff = await SniffAsync(file, ct);
        if (sniff == null)
            return BadRequest("File type is not allowed.");

        var max = sniff.Kind == SniffedKind.Image ? MaxImageBytes : MaxVideoBytes;
        if (file.Length > max)
            return BadRequest($"File is too large. Max allowed: {max / (1024 * 1024)} MB.");

        var safeContentType = sniff.ContentType;
        var safeExt = sniff.Extension;

        var safeOriginalName = NormalizeFileNameKeepBase(originalName, safeExt);

        var id = Guid.NewGuid();
        var day = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var objectKey = $"attachments/{day}/{id}{safeExt}";

        await using (var stream = file.OpenReadStream())
        {
            await _storage.PutAsync(objectKey, stream, safeContentType, ct);
        }

        var entity = new UniChat.Domain.Entities.Attachment
        {
            Id = id,
            UploaderId = me,
            MessageId = null,
            FileName = safeOriginalName,
            ContentType = safeContentType,
            Size = file.Length,
            StoragePath = objectKey,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Attachments.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(new AttachmentDto(entity.Id, entity.FileName, entity.ContentType, entity.Size, entity.CreatedAt));
    }


    // GET /api/attachments/{id}

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
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
            .SingleOrDefaultAsync(ct);

        if (att == null) return NotFound();

        
        if (att.MessageId == null)
        {
            if (att.UploaderId != me) return Forbid();
        }
        else
        {
            
            var conversationId = await _db.Messages
                .Where(m => m.Id == att.MessageId.Value)
                .Select(m => (Guid?)m.ConversationId)
                .SingleOrDefaultAsync(ct);

            if (conversationId == null) return NotFound();

            var isMember = await _db.Memberships
                .AnyAsync(ms => ms.ConversationId == conversationId.Value && ms.UserId == me, ct);

            if (!isMember) return Forbid();
        }

        try
        {
            
            var (stream, contentType) = await _storage.GetAsync(att.StoragePath, ct);

            
            var ctToUse = string.IsNullOrWhiteSpace(att.ContentType) ? contentType : att.ContentType;

            
            return File(stream, ctToUse, att.FileName, enableRangeProcessing: true);
        }
        catch
        {
            
            return NotFound("File missing in storage.");
        }
    }

    // ===== hardening helpers (C# 12 compatible) =====

    private enum SniffedKind { Image, Video }

    private sealed record SniffedFile(SniffedKind Kind, string ContentType, string Extension);

    private static async Task<SniffedFile?> SniffAsync(IFormFile file, CancellationToken ct)
    {
        // читаем первые 64 байта
        var header = new byte[64];
        int read;

        await using (var s = file.OpenReadStream())
        {
            read = await ReadAtMostAsync(s, header, 0, header.Length, ct);
            if (s.CanSeek) s.Seek(0, SeekOrigin.Begin);
        }

        if (read <= 0) return null;

        // ===== Images =====

        // JPEG: FF D8 FF
        if (StartsWith(header, read, 0xFF, 0xD8, 0xFF))
            return new SniffedFile(SniffedKind.Image, "image/jpeg", ".jpg");

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (StartsWith(header, read, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
            return new SniffedFile(SniffedKind.Image, "image/png", ".png");

        // GIF: GIF87a / GIF89a
        if (StartsWithAscii(header, read, "GIF87a") || StartsWithAscii(header, read, "GIF89a"))
            return new SniffedFile(SniffedKind.Image, "image/gif", ".gif");

        // WEBP: "RIFF" .... "WEBP" (offset 0 and 8)
        if (read >= 12 && StartsWithAscii(header, read, "RIFF") && AsciiAt(header, read, 8, "WEBP"))
            return new SniffedFile(SniffedKind.Image, "image/webp", ".webp");

        // ===== Video =====

        // MP4/MOV: "ftyp" at offset 4
        if (read >= 12 && AsciiAt(header, read, 4, "ftyp"))
        {
            // brand at 8..11
            var brand = GetAscii(header, read, 8, 4).ToLowerInvariant();

            if (brand == "qt  ")
                return new SniffedFile(SniffedKind.Video, "video/quicktime", ".mov");

            return new SniffedFile(SniffedKind.Video, "video/mp4", ".mp4");
        }

        // EBML (MKV/WEBM): 1A 45 DF A3
        if (StartsWith(header, read, 0x1A, 0x45, 0xDF, 0xA3))
        {
            // Минимально безопасно: принимаем как mkv контейнер
            
            return new SniffedFile(SniffedKind.Video, "video/x-matroska", ".mkv");
        }

        return null;
    }

    private static string NormalizeFileNameKeepBase(string originalName, string extWithDot)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "file";

        baseName = new string(baseName.Where(ch => !char.IsControl(ch)).ToArray()).Trim();

        if (baseName.Length == 0) baseName = "file";
        if (baseName.Length > 120) baseName = baseName[..120];

        return baseName + extWithDot;
    }

    private static async Task<int> ReadAtMostAsync(Stream s, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var total = 0;
        while (total < count)
        {
            var n = await s.ReadAsync(buffer.AsMemory(offset + total, count - total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private static bool StartsWith(byte[] data, int len, params byte[] prefix)
    {
        if (len < prefix.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
            if (data[i] != prefix[i]) return false;
        return true;
    }

    private static bool StartsWithAscii(byte[] data, int len, string ascii)
    {
        if (len < ascii.Length) return false;
        for (int i = 0; i < ascii.Length; i++)
            if (data[i] != (byte)ascii[i]) return false;
        return true;
    }

    private static bool AsciiAt(byte[] data, int len, int offset, string ascii)
    {
        if (offset < 0) return false;
        if (len < offset + ascii.Length) return false;
        for (int i = 0; i < ascii.Length; i++)
            if (data[offset + i] != (byte)ascii[i]) return false;
        return true;
    }

    private static string GetAscii(byte[] data, int len, int offset, int length)
    {
        if (offset < 0) return "";
        if (len < offset + length) return "";
        return System.Text.Encoding.ASCII.GetString(data, offset, length);
    }
}
