namespace UniChat.Api.Contracts.Users;

public record UserDto(Guid Id, string UserName, DateTimeOffset CreatedAt);
