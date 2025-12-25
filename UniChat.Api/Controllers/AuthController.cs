using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using UniChat.Api.Auth;
using UniChat.Api.Contracts.Auth;
using UniChat.Api.Contracts.Users;
using UniChat.Api.Services;
using UniChat.Domain.Entities;
using UniChat.Infrastructure.Persistence;

namespace UniChat.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UniChatDbContext _db;
    private readonly IJwtTokenService _jwt;

    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public AuthController(UniChatDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var userName = (req.UserName ?? "").Trim().ToLowerInvariant();
        var password = req.Password ?? "";

        if (userName.Length < 3) return BadRequest("Username must be at least 3 characters.");
        if (password.Length < 6) return BadRequest("Password must be at least 6 characters.");

        var exists = await _db.Users.AnyAsync(x => x.UserName == userName);
        if (exists) return Conflict("Username already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Username already taken.");
        }

        var access = _jwt.CreateAccessToken(user);
        var refresh = await IssueRefreshToken(user.Id);

        return Ok(new AuthResponse(access, refresh));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var userName = (req.UserName ?? "").Trim().ToLowerInvariant();
        var password = req.Password ?? "";

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        if (user == null) return Unauthorized();

        var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!ok) return Unauthorized();

        var access = _jwt.CreateAccessToken(user);
        var refresh = await IssueRefreshToken(user.Id);

        return Ok(new AuthResponse(access, refresh));
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var token = (req.RefreshToken ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("RefreshToken is required.");

        var tokenHash = HashToken(token);

        var row = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (row == null) return Unauthorized();

        if (row.RevokedAt != null) return Unauthorized();
        if (row.ExpiresAt <= DateTimeOffset.UtcNow) return Unauthorized();

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == row.UserId);
        if (user == null) return Unauthorized();

        // ✅ ротация: старый отзываем, выдаём новый
        var newRaw = GenerateRawToken();
        var newHash = HashToken(newRaw);

        row.RevokedAt = DateTimeOffset.UtcNow;
        row.ReplacedByTokenHash = newHash;

        var newRow = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshLifetime)
        };

        _db.RefreshTokens.Add(newRow);
        await _db.SaveChangesAsync();

        var access = _jwt.CreateAccessToken(user);
        return Ok(new AuthResponse(access, newRaw));
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        var token = (req.RefreshToken ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("RefreshToken is required.");

        var hash = HashToken(token);

        var row = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (row == null) return NoContent();

        row.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // OPTIONAL: GET /api/auth/me
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        return Ok(new UserDto(user.Id, user.UserName, user.CreatedAt));
    }

    // ===== helpers =====

    private async Task<string> IssueRefreshToken(Guid userId)
    {
        var raw = GenerateRawToken();
        var hash = HashToken(raw);

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshLifetime)
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();

        return raw;
    }

    private static string GenerateRawToken()
    {
        // 32 байта = 256 бит
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes); // удобно передавать в JSON
    }

    private static string HashToken(string rawToken)
    {
        // SHA-256(token) как fingerprint
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // uppercase hex
    }
}
