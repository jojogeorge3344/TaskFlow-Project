namespace TaskFlow.Application.Auth;

public record LoginRequest(string EmailOrUsername, string Password);
