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
    private readonly IWebHostEnvironment _env;

    public UsersController(UniChatDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // GET /api/users  список всех)
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        if (!_env.IsDevelopment())
            return NotFound(); // чтобы не светить endpoint в проде

        var users = await _db.Users
            .OrderBy(u => u.UserName)
            .Select(u => new UserDto(u.Id, u.UserName, u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/users/search?q=alex&take=20
    [HttpGet("search")]
    public async Task<ActionResult<List<UserSearchItemDto>>> Search([FromQuery] string q, [FromQuery] int take = 20)
    {
        var me = User.GetUserId();

        q = (q ?? "").Trim().ToLowerInvariant();
        if (q.Length < 2) return Ok(new List<UserSearchItemDto>());

        take = Math.Clamp(take, 1, 50);

        
        // Если нужен поиск "по подстроке" — .Contains(q)
        var users = await _db.Users
            .Where(u => u.Id != me)
            .Where(u => u.UserName.StartsWith(q))
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
            .Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.UserName, u.CreatedAt))
            .SingleOrDefaultAsync();

        if (user == null) return NotFound();
        return Ok(user);
    }
}
