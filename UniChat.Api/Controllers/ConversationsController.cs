using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Conversations;
using UniChat.Api.Hubs;
using UniChat.Api.Services;
using UniChat.Domain.Entities;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly UniChatDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IFileStorage _storage;

    public ConversationsController(UniChatDbContext db, IHubContext<ChatHub> hub, IFileStorage storage)
    {
        _db = db;
        _hub = hub;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConversationListItemDto>>> GetMy()
    {
        var me = User.GetUserId();

        var result = await _db.Conversations
            .Where(c => _db.Memberships.Any(m => m.ConversationId == c.Id && m.UserId == me))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Title,
                c.OwnerId,
                c.CreatedAt,

                LastReadAt = _db.Memberships
                    .Where(m => m.ConversationId == c.Id && m.UserId == me)
                    .Select(m => m.LastReadAt)
                    .FirstOrDefault(),

                LastMsgText = _db.Messages
                    .Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Text)
                    .FirstOrDefault(),

                LastMsgAt = _db.Messages
                    .Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => (DateTimeOffset?)m.CreatedAt)
                    .FirstOrDefault()
            })
            .Select(x => new ConversationListItemDto(
                x.Id,
                x.Type,
                x.Title,
                x.OwnerId,
                x.CreatedAt,
                _db.Messages.Count(m =>
                    m.ConversationId == x.Id &&
                    m.SenderId != me &&
                    (x.LastReadAt == null || m.CreatedAt > x.LastReadAt)
                ),
                x.LastMsgText,
                x.LastMsgAt
            ))
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailsDto>> GetDetails(Guid conversationId)
    {
        var me = User.GetUserId();

        var isMember = await _db.Memberships.AnyAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (!isMember) return Forbid();

        var conv = await _db.Conversations
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Id, c.Type, c.Title, c.OwnerId, c.CreatedAt })
            .SingleOrDefaultAsync();

        if (conv == null) return NotFound();

        var members = await _db.Memberships
            .Where(m => m.ConversationId == conversationId)
            .Join(_db.Users,
                m => m.UserId,
                u => u.Id,
                (m, u) => new { m, u })
            .OrderBy(x => x.u.UserName)
            .Select(x => new MemberDto(
                x.u.Id,
                x.u.UserName,
                x.m.Role,
                x.m.Permissions,
                x.m.JoinedAt,
                x.m.LastReadAt
            ))
            .ToListAsync();

        return Ok(new ConversationDetailsDto(conv.Id, conv.Type, conv.Title, conv.OwnerId, conv.CreatedAt, members));
    }

    [HttpPost]
    public async Task<ActionResult<ConversationDto>> Create(CreateConversationRequest req)
    {
        var me = User.GetUserId();

        var title = (req.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title is required.");

        var memberIds = (req.MemberIds ?? new List<Guid>()).ToList();
        memberIds = memberIds.Where(x => x != Guid.Empty).Distinct().ToList();

        if (req.Type == ConversationType.Direct)
        {
            if (memberIds.Count != 1) return BadRequest("Direct chat requires exactly 1 memberId.");
            var otherId = memberIds[0];
            if (otherId == me) return BadRequest("Cannot create direct chat with yourself.");

            var existing = await _db.Conversations
                .Where(c => c.Type == ConversationType.Direct)
                .Where(c =>
                    _db.Memberships.Any(m => m.ConversationId == c.Id && m.UserId == me) &&
                    _db.Memberships.Any(m => m.ConversationId == c.Id && m.UserId == otherId))
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new ConversationDto(c.Id, c.Type, c.Title, c.OwnerId, c.CreatedAt))
                .FirstOrDefaultAsync();

            if (existing != null) return Ok(existing);

            var conv = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Direct,
                Title = "direct",
                OwnerId = null,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Conversations.Add(conv);

            _db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                ConversationId = conv.Id,
                UserId = me,
                Role = MemberRole.Member,
                Permissions = ChannelPermissions.Write,
                JoinedAt = DateTimeOffset.UtcNow
            });

            _db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                ConversationId = conv.Id,
                UserId = otherId,
                Role = MemberRole.Member,
                Permissions = ChannelPermissions.Write,
                JoinedAt = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new ConversationDto(conv.Id, conv.Type, conv.Title, conv.OwnerId, conv.CreatedAt));
        }

        if (!memberIds.Contains(me)) memberIds.Add(me);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = req.Type,
            Title = title,
            OwnerId = req.Type == ConversationType.Channel ? me : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Conversations.Add(conversation);

        foreach (var userId in memberIds)
        {
            var isOwner = req.Type == ConversationType.Channel && userId == me;

            _db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = userId,
                Role = isOwner ? MemberRole.Owner : MemberRole.Member,
                Permissions = isOwner
                    ? (ChannelPermissions.Write |
                       ChannelPermissions.Invite |
                       ChannelPermissions.ManageRoles |
                       ChannelPermissions.DeleteMessages |
                       ChannelPermissions.ManageChannel)
                    : ChannelPermissions.Write,
                JoinedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new ConversationDto(conversation.Id, conversation.Type, conversation.Title, conversation.OwnerId, conversation.CreatedAt));
    }

    [HttpPost("direct/{otherUserId:guid}")]
    public Task<ActionResult<ConversationDto>> CreateDirect(Guid otherUserId)
    {
        return Create(new CreateConversationRequest(
            Type: ConversationType.Direct,
            Title: "direct",
            MemberIds: new List<Guid> { otherUserId }
        ));
    }

    [HttpPost("{conversationId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid conversationId, AddMemberRequest req)
    {
        var me = User.GetUserId();
        if (req.UserId == Guid.Empty) return BadRequest("UserId is required.");

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type == ConversationType.Direct)
            return BadRequest("Cannot add members to direct conversation.");

        var my = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (my == null) return Forbid();

        if (conv.Type == ConversationType.Channel && !Has(my.Permissions, ChannelPermissions.Invite))
            return Forbid();

        var exists = await _db.Memberships.AnyAsync(m => m.ConversationId == conversationId && m.UserId == req.UserId);
        if (exists) return Conflict("User is already a member.");

        var userExists = await _db.Users.AnyAsync(u => u.Id == req.UserId);
        if (!userExists) return BadRequest("User not found.");

        _db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            UserId = req.UserId,
            Role = MemberRole.Member,
            Permissions = ChannelPermissions.Write,
            JoinedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("member_updated", new
        {
            conversationId,
            userId = req.UserId,
            action = "added"
        });

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("conversation_updated", new
        {
            conversationId,
            action = "members_changed"
        });

        return NoContent();
    }

    [HttpPatch("{conversationId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid conversationId, Guid userId, UpdateMemberRequest req)
    {
        var me = User.GetUserId();

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type != ConversationType.Channel)
            return BadRequest("Roles/permissions are supported only for channels.");

        var my = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (my == null) return Forbid();

        if (!Has(my.Permissions, ChannelPermissions.ManageRoles))
            return Forbid();

        var target = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId);
        if (target == null) return NotFound();

        // нельзя менять owner через этот endpoint
        if (target.Role == MemberRole.Owner)
            return BadRequest("Cannot change owner. Use transfer ownership endpoint.");

        target.Role = req.Role;
        target.Permissions = req.Permissions;

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("member_updated", new
        {
            conversationId,
            userId,
            action = "updated",
            role = target.Role.ToString(),
            permissions = (int)target.Permissions
        });

        return NoContent();
    }

    [HttpDelete("{conversationId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid conversationId, Guid userId)
    {
        var me = User.GetUserId();

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type == ConversationType.Direct)
            return BadRequest("Cannot remove members from direct conversation.");

        var my = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (my == null) return Forbid();

        if (conv.Type == ConversationType.Channel && !Has(my.Permissions, ChannelPermissions.ManageRoles))
            return Forbid();

        var target = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId);
        if (target == null) return NotFound();

        if (target.Role == MemberRole.Owner)
            return BadRequest("Cannot remove owner. Transfer ownership first.");

        _db.Memberships.Remove(target);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("member_updated", new
        {
            conversationId,
            userId,
            action = "removed"
        });

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("conversation_updated", new
        {
            conversationId,
            action = "members_changed"
        });

        return NoContent();
    }

    [HttpPost("{conversationId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid conversationId)
    {
        var me = User.GetUserId();

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type == ConversationType.Direct)
            return BadRequest("Cannot leave direct conversation.");

        var my = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (my == null) return NoContent();

        if (my.Role == MemberRole.Owner)
            return BadRequest("Owner cannot leave. Transfer ownership or delete channel.");

        _db.Memberships.Remove(my);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("member_updated", new
        {
            conversationId,
            userId = me,
            action = "left"
        });

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("conversation_updated", new
        {
            conversationId,
            action = "members_changed"
        });

        return NoContent();
    }

    // POST /api/conversations/{id}/transfer-ownership
    [HttpPost("{conversationId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid conversationId, TransferOwnershipRequest req)
    {
        var me = User.GetUserId();
        if (req.NewOwnerId == Guid.Empty) return BadRequest("NewOwnerId is required.");

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type != ConversationType.Channel)
            return BadRequest("Ownership can be transferred only for channels.");

        if (conv.OwnerId != me)
            return Forbid();

        var newOwner = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == req.NewOwnerId);
        if (newOwner == null) return BadRequest("New owner must be a member of this channel.");

        // текущий owner membership
        var oldOwner = await _db.Memberships.SingleAsync(m => m.ConversationId == conversationId && m.UserId == me);

        // обновляем conversation
        conv.OwnerId = req.NewOwnerId;

        // старого owner сделаем Admin со всеми правами 
        oldOwner.Role = MemberRole.Admin;
        oldOwner.Permissions = (ChannelPermissions.Write |
                                ChannelPermissions.Invite |
                                ChannelPermissions.ManageRoles |
                                ChannelPermissions.DeleteMessages |
                                ChannelPermissions.ManageChannel);

        // нового owner
        newOwner.Role = MemberRole.Owner;
        newOwner.Permissions = (ChannelPermissions.Write |
                                ChannelPermissions.Invite |
                                ChannelPermissions.ManageRoles |
                                ChannelPermissions.DeleteMessages |
                                ChannelPermissions.ManageChannel);

        await _db.SaveChangesAsync();

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("member_updated", new
        {
            conversationId,
            userId = req.NewOwnerId,
            action = "ownership_transferred"
        });

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("conversation_updated", new
        {
            conversationId,
            action = "ownership_changed",
            ownerId = req.NewOwnerId
        });

        return NoContent();
    }

    //  DELETE /api/conversations/{conversationId} (каскадное удаление)
    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId)
    {
        var me = User.GetUserId();

        var conv = await _db.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        if (conv.Type == ConversationType.Direct)
            return BadRequest("Direct conversations cannot be deleted (for now).");

        var my = await _db.Memberships.SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == me);
        if (my == null) return Forbid();

        if (conv.Type == ConversationType.Channel)
        {
            if (conv.OwnerId != me) return Forbid();
        }
        else
        {
            if (!Has(my.Permissions, ChannelPermissions.ManageChannel))
                return Forbid();
        }

        // Собираем список объектов в хранилище, которые нужно удалить
        var attachmentRows = await _db.Attachments
            .Where(a => a.MessageId != null &&
                        _db.Messages.Any(m => m.Id == a.MessageId.Value && m.ConversationId == conversationId))
            .Select(a => new { a.Id, a.StoragePath })
            .ToListAsync();

        var attIds = attachmentRows.Select(x => x.Id).ToList();
        if (attIds.Count > 0)
        {
            var atts = await _db.Attachments.Where(a => attIds.Contains(a.Id)).ToListAsync();
            _db.Attachments.RemoveRange(atts);
        }

        var msgs = await _db.Messages.Where(m => m.ConversationId == conversationId).ToListAsync();
        _db.Messages.RemoveRange(msgs);

        var mems = await _db.Memberships.Where(m => m.ConversationId == conversationId).ToListAsync();
        _db.Memberships.RemoveRange(mems);

        _db.Conversations.Remove(conv);

        await _db.SaveChangesAsync();

        // Удаляем объекты из MinIO (best-effort)
        foreach (var a in attachmentRows)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(a.StoragePath))
                    await _storage.DeleteAsync(a.StoragePath);
            }
            catch
            {
                // best-effort
            }
        }

        await _hub.Clients.Group(conversationId.ToString()).SendAsync("conversation_deleted", new
        {
            conversationId
        });

        return NoContent();
    }

    private static bool Has(ChannelPermissions value, ChannelPermissions flag) => (value & flag) == flag;
}
