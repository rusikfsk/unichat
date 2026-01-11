namespace UniChat.Api.Contracts.Auth;

public record RegisterRequest(
    string UserName,
    string Password,
    string Email,
    string DisplayName
);
