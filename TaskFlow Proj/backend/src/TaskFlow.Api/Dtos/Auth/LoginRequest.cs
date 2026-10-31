namespace TaskFlow.Api.Dtos.Auth;

public record LoginRequest(string EmailOrUsername, string Password);
