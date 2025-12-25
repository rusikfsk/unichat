using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniChat.Domain.Entities;

namespace UniChat.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public string Secret { get; init; } = default!;
    public int ExpiresMinutes { get; init; } = 60;
}

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;
    public JwtTokenService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public string CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opt.ExpiresMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
