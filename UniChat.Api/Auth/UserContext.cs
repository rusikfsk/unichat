using System.Security.Claims;

namespace UniChat.Api.Auth;

public static class UserContext
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub"); // JwtRegisteredClaimNames.Sub

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var id))
            throw new InvalidOperationException("User id claim is missing or invalid.");

        return id;
    }
}
