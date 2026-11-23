using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Abstractions;

/// <summary>Creates the signed access token (JWT) for an authenticated user.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(UserDto user);
}
