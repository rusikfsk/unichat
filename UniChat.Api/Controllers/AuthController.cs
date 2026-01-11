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
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private const int MaxActiveRefreshTokensPerUser = 5;

    
    public AuthController(UniChatDbContext db, IJwtTokenService jwt, IEmailSender email, IConfiguration cfg)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _cfg = cfg;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var userName = (req.UserName ?? "").Trim().ToLowerInvariant();
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var displayName = (req.DisplayName ?? "").Trim();
        var password = req.Password ?? "";

        if (userName.Length < 3) return BadRequest("Username must be at least 3 characters.");
        if (password.Length < 6) return BadRequest("Password must be at least 6 characters.");
        if (displayName.Length < 2) return BadRequest("DisplayName must be at least 2 characters.");
        if (!IsValidEmail(email)) return BadRequest("Email is invalid.");

        if (await _db.Users.AnyAsync(x => x.UserName == userName))
            return Conflict("Username already taken.");

        if (await _db.Users.AnyAsync(x => x.Email == email))
            return Conflict("Email already taken.");

        // email confirm token
        var confirmRaw = GenerateRawToken();
        var confirmHash = HashToken(confirmRaw);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            DisplayName = displayName,
            EmailConfirmedAt = null,
            EmailConfirmationTokenHash = confirmHash,
            AvatarAttachmentId = null,
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
            return Conflict("Username or email already taken.");
        }

        
        await SendConfirmEmail(user, confirmRaw);

        
        return Ok(new AuthResponse(AccessToken: "", RefreshToken: ""));
    }

    // POST /api/auth/confirm-email
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        if (req.UserId == Guid.Empty) return BadRequest("UserId is required.");
        var token = (req.Token ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("Token is required.");

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == req.UserId);
        if (user == null) return NotFound();

        if (user.EmailConfirmedAt != null)
            return NoContent(); // already confirmed

        if (string.IsNullOrWhiteSpace(user.EmailConfirmationTokenHash))
            return BadRequest("Confirmation token is missing. Please request resend.");

        var hash = HashToken(token);
        if (!FixedTimeEquals(user.EmailConfirmationTokenHash, hash))
            return Unauthorized();

        user.EmailConfirmedAt = DateTimeOffset.UtcNow;
        user.EmailConfirmationTokenHash = null;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var userName = (req.UserName ?? "").Trim().ToLowerInvariant();
        var password = req.Password ?? "";

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        if (user == null) return Unauthorized();

        
        if (user.EmailConfirmedAt == null) return Unauthorized();

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
        if (user.EmailConfirmedAt == null) return Unauthorized();

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

        await CleanupRefreshTokens(user.Id);

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

        await CleanupRefreshTokens(row.UserId);

        return NoContent();
    }

    // GET /api/auth/me
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.SingleAsync(u => u.Id == userId);
        return Ok(new UserDto(user.Id, user.UserName, user.CreatedAt));
    }

    // ===== helpers =====

    private async Task SendConfirmEmail(User user, string rawToken)
    {
        var publicUrl = (_cfg["App:PublicUrl"] ?? "").Trim().TrimEnd('/');
        
        var link = $"{publicUrl}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(rawToken)}";

        var subject = "Confirm your email";
        var body = $@"
            <p>Hello, {System.Net.WebUtility.HtmlEncode(user.DisplayName)}!</p>
            <p>Please confirm your email by clicking this link:</p>
            <p><a href=""{link}"">{link}</a></p>
            <p>If you did not register — ignore this email.</p>";

        await _email.SendAsync(user.Email, subject, body);
    }

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

        await CleanupRefreshTokens(userId);
        return raw;
    }

    private async Task CleanupRefreshTokens(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;

        var junk = await _db.RefreshTokens
            .Where(t => t.UserId == userId)
            .Where(t => t.ExpiresAt <= now || t.RevokedAt != null)
            .ToListAsync();

        if (junk.Count > 0)
            _db.RefreshTokens.RemoveRange(junk);

        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId)
            .Where(t => t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        if (active.Count > MaxActiveRefreshTokensPerUser)
        {
            var toRemove = active.Skip(MaxActiveRefreshTokensPerUser).ToList();
            _db.RefreshTokens.RemoveRange(toRemove);
        }

        await _db.SaveChangesAsync();
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        
        var ba = Encoding.ASCII.GetBytes(a);
        var bb = Encoding.ASCII.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch { return false; }
    }
}
