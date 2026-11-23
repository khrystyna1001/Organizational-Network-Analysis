namespace ONA.Identity.Application.DTOs;

/// <summary>A signed access token and its lifetime in seconds.</summary>
public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);
