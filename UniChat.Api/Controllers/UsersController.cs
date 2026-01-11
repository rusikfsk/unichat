using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Users;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly UniChatDbContext _db;

    public UsersController(UniChatDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        // env больше не нужен для dev-only
    }

    // GET /api/users?skip=0&take=200&q=alex
    // Список пользователей (всегда, не dev-only)
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200)
    {
        var me = User.GetUserId();

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);

        q = (q ?? "").Trim().ToLowerInvariant();

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.Id != me);

        // Если q пустая — возвращаем всех (с пагинацией).
        // Если q задана — фильтруем по началу UserName.
        if (q.Length > 0)
        {
            query = query.Where(u => u.UserName.ToLower().StartsWith(q));
        }

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip(skip)
            .Take(take)
            .Select(u => new UserDto(u.Id, u.UserName, u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/users/search?q=alex&take=20
    // (оставляем как быстрый автокомплит, min length 2)
    [HttpGet("search")]
    public async Task<ActionResult<List<UserSearchItemDto>>> Search([FromQuery] string q, [FromQuery] int take = 20)
    {
        var me = User.GetUserId();

        q = (q ?? "").Trim().ToLowerInvariant();
        if (q.Length < 2) return Ok(new List<UserSearchItemDto>());

        take = Math.Clamp(take, 1, 50);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id != me)
            .Where(u => u.UserName.ToLower().StartsWith(q))
            .OrderBy(u => u.UserName)
            .Take(take)
            .Select(u => new UserSearchItemDto(u.Id, u.UserName))
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.UserName, u.CreatedAt))
            .SingleOrDefaultAsync();

        if (user == null) return NotFound();
        return Ok(user);
    }

    // PATCH /api/users/me/display-name
    [HttpPatch("me/display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest req)
    {
        var me = User.GetUserId();

        var displayName = (req.DisplayName ?? "").Trim();
        if (displayName.Length < 2) return BadRequest("DisplayName must be at least 2 characters.");
        if (displayName.Length > 50) return BadRequest("DisplayName is too long.");

        var user = await _db.Users.SingleAsync(u => u.Id == me);
        user.DisplayName = displayName;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/users/me/avatar
    // Avatar = attachmentId (картинка), которую залил этот пользователь и она ещё не привязана к сообщению
    [HttpPatch("me/avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest req)
    {
        var me = User.GetUserId();
        if (req.AttachmentId == Guid.Empty) return BadRequest("AttachmentId is required.");

        var att = await _db.Attachments
            .AsNoTracking()
            .Where(a => a.Id == req.AttachmentId)
            .Select(a => new { a.Id, a.UploaderId, a.MessageId, a.ContentType })
            .SingleOrDefaultAsync();

        if (att == null) return BadRequest("Attachment not found.");
        if (att.UploaderId != me) return Forbid();
        if (att.MessageId != null) return BadRequest("Attachment already bound to a message.");
        if (!att.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Avatar must be an image.");

        var user = await _db.Users.SingleAsync(u => u.Id == me);
        user.AvatarAttachmentId = att.Id;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
