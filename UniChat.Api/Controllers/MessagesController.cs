using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Attachments;
using UniChat.Api.Contracts.Messages;
using UniChat.Api.Hubs;
using UniChat.Domain.Entities;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController : ControllerBase
{
    private readonly UniChatDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public MessagesController(UniChatDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // GET /api/messages?conversationId=...&take=50&beforeMessageId=...
    [HttpGet]
    public async Task<ActionResult<List<MessageDto>>> Get(
        [FromQuery] Guid conversationId,
        [FromQuery] int take = 50,
        [FromQuery] Guid? beforeMessageId = null)
    {
        if (conversationId == Guid.Empty) return BadRequest("conversationId is required.");
        take = Math.Clamp(take, 1, 200);

        var me = User.GetUserId();

        var membership = await _db.Memberships
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);

        if (membership == null) return Forbid();

        DateTimeOffset? beforeCreatedAt = null;

        // Если задан beforeMessageId — найдём CreatedAt этого сообщения и будем брать строго "старее"
        if (beforeMessageId.HasValue && beforeMessageId.Value != Guid.Empty)
        {
            var anchor = await _db.Messages
                .Where(m => m.ConversationId == conversationId && m.Id == beforeMessageId.Value)
                .Select(m => new { m.CreatedAt, m.Id })
                .SingleOrDefaultAsync();

            if (anchor == null)
                return BadRequest("beforeMessageId not found in this conversation.");

            beforeCreatedAt = anchor.CreatedAt;
        }

        // Берём по убыванию (страница формируется сверху вниз), потом развернём в хронологию
        var query = _db.Messages
            .Where(m => m.ConversationId == conversationId);

        if (beforeCreatedAt.HasValue)
        {
            // строго старее чем anchor:
            // 1) CreatedAt < anchor.CreatedAt
            // 2) на случай совпадения CreatedAt (редко) — используем Id как tie-breaker:
            query = query.Where(m =>
                m.CreatedAt < beforeCreatedAt.Value ||
                (m.CreatedAt == beforeCreatedAt.Value && m.Id != beforeMessageId!.Value));
        }

        var page = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.ConversationId,
                m.SenderId,
                m.Text,
                m.CreatedAt,
                SenderUserName = _db.Users
                    .Where(u => u.Id == m.SenderId)
                    .Select(u => u.UserName)
                    .FirstOrDefault()!
            })
            .ToListAsync();

        // переворачиваем в хронологическом порядке
        page.Reverse();

        var messageIds = page.Select(x => x.Id).ToList();

        var attachments = await _db.Attachments
            .Where(a => a.MessageId != null && messageIds.Contains(a.MessageId.Value))
            .Select(a => new
            {
                a.Id,
                a.MessageId,
                a.FileName,
                a.ContentType,
                a.Size,
                a.CreatedAt
            })
            .ToListAsync();

        var attByMsg = attachments
            .GroupBy(a => a.MessageId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.Size, x.CreatedAt)).ToList()
            );

        var dtos = page
            .Select(x => new MessageDto(
                x.Id,
                x.ConversationId,
                x.SenderId,
                x.SenderUserName ?? "",
                x.Text,
                x.CreatedAt,
                attByMsg.TryGetValue(x.Id, out var list) ? list : new List<AttachmentDto>()
            ))
            .ToList();

        // Авто-read только когда грузим "последние" (т.е. без beforeMessageId)
        // Иначе при скролле вверх читать "старые" не нужно.
        if (dtos.Count > 0 && !beforeMessageId.HasValue)
        {
            var lastAt = dtos[^1].CreatedAt;
            if (membership.LastReadAt == null || lastAt > membership.LastReadAt)
            {
                membership.LastReadAt = lastAt;
                await _db.SaveChangesAsync();

                await _hub.Clients.Group(conversationId.ToString())
                    .SendAsync("read", new
                    {
                        conversationId,
                        userId = me,
                        readAt = membership.LastReadAt
                    });
            }
        }

        return Ok(dtos);
    }

    // POST /api/messages
    [HttpPost]
    public async Task<ActionResult<MessageDto>> Create(CreateMessageRequest req)
    {
        var me = User.GetUserId();

        if (req.ConversationId == Guid.Empty) return BadRequest("ConversationId is required.");

        var text = (req.Text ?? "").Trim();
        if (text.Length > 4000) return BadRequest("Text is too long.");

        var attachmentIds = (req.AttachmentIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (string.IsNullOrWhiteSpace(text) && attachmentIds.Count == 0)
            return BadRequest("Message must have text or attachments.");

        var conv = await _db.Conversations
            .Where(c => c.Id == req.ConversationId)
            .Select(c => new { c.Id, c.Type })
            .SingleOrDefaultAsync();

        if (conv == null) return NotFound("Conversation not found.");

        var membership = await _db.Memberships
            .SingleOrDefaultAsync(m => m.ConversationId == req.ConversationId && m.UserId == me);

        if (membership == null) return Forbid();

        if (conv.Type == ConversationType.Channel &&
            (membership.Permissions & ChannelPermissions.Write) != ChannelPermissions.Write)
            return Forbid();

        var senderName = await _db.Users
            .Where(u => u.Id == me)
            .Select(u => u.UserName)
            .SingleAsync();

        List<Attachment> attsToBind = new();
        if (attachmentIds.Count > 0)
        {
            attsToBind = await _db.Attachments
                .Where(a => attachmentIds.Contains(a.Id))
                .ToListAsync();

            if (attsToBind.Count != attachmentIds.Count)
                return BadRequest("Some attachments were not found.");

            if (attsToBind.Any(a => a.UploaderId != me))
                return Forbid();

            if (attsToBind.Any(a => a.MessageId != null))
                return BadRequest("Some attachments are already bound to a message.");
        }

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = req.ConversationId,
            SenderId = me,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Messages.Add(msg);

        foreach (var a in attsToBind)
            a.MessageId = msg.Id;

        if (membership.LastReadAt == null || msg.CreatedAt > membership.LastReadAt)
            membership.LastReadAt = msg.CreatedAt;

        await _db.SaveChangesAsync();

        var attDtos = attsToBind
            .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.CreatedAt))
            .ToList();

        var dto = new MessageDto(
            msg.Id,
            msg.ConversationId,
            msg.SenderId,
            senderName,
            msg.Text,
            msg.CreatedAt,
            attDtos);

        await _hub.Clients.Group(req.ConversationId.ToString())
            .SendAsync("message", dto);

        await _hub.Clients.Group(req.ConversationId.ToString())
            .SendAsync("read", new
            {
                conversationId = req.ConversationId,
                userId = me,
                readAt = membership.LastReadAt
            });

        return Ok(dto);
    }

    // DELETE /api/messages/{messageId}
    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId)
    {
        var me = User.GetUserId();

        var msg = await _db.Messages.SingleOrDefaultAsync(m => m.Id == messageId);
        if (msg == null) return NotFound();

        var membership = await _db.Memberships
            .SingleOrDefaultAsync(m => m.ConversationId == msg.ConversationId && m.UserId == me);

        if (membership == null) return Forbid();

        var isOwnerOfMessage = msg.SenderId == me;
        if (!isOwnerOfMessage)
        {
            if ((membership.Permissions & ChannelPermissions.DeleteMessages) != ChannelPermissions.DeleteMessages)
                return Forbid();
        }

        var atts = await _db.Attachments
            .Where(a => a.MessageId == msg.Id)
            .ToListAsync();

        _db.Attachments.RemoveRange(atts);
        _db.Messages.Remove(msg);

        await _db.SaveChangesAsync();

        foreach (var a in atts)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(a.StoragePath) && System.IO.File.Exists(a.StoragePath))
                    System.IO.File.Delete(a.StoragePath);
            }
            catch { }
        }

        await _hub.Clients.Group(msg.ConversationId.ToString())
            .SendAsync("message_deleted", new
            {
                conversationId = msg.ConversationId,
                messageId = msg.Id
            });

        return NoContent();
    }

    // POST /api/messages/read
    public record MarkReadRequest(Guid ConversationId, Guid? LastMessageId);

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequest req)
    {
        var me = User.GetUserId();

        if (req.ConversationId == Guid.Empty) return BadRequest("ConversationId is required.");

        var membership = await _db.Memberships
            .SingleOrDefaultAsync(m => m.ConversationId == req.ConversationId && m.UserId == me);

        if (membership == null) return Forbid();

        DateTimeOffset readAt = DateTimeOffset.UtcNow;

        if (req.LastMessageId.HasValue)
        {
            var found = await _db.Messages
                .Where(x => x.ConversationId == req.ConversationId && x.Id == req.LastMessageId.Value)
                .Select(x => new { x.CreatedAt })
                .SingleOrDefaultAsync();

            if (found == null) return BadRequest("LastMessageId not found in this conversation.");

            readAt = found.CreatedAt;
        }

        if (membership.LastReadAt == null || readAt > membership.LastReadAt)
            membership.LastReadAt = readAt;

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(req.ConversationId.ToString())
            .SendAsync("read", new
            {
                conversationId = req.ConversationId,
                userId = me,
                readAt = membership.LastReadAt
            });

        return NoContent();
    }
}
