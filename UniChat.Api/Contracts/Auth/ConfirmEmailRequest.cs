namespace UniChat.Api.Contracts.Auth;

public record ConfirmEmailRequest(Guid UserId, string Token);
