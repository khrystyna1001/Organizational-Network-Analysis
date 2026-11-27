using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Application.Features.Users.Commands;
using ONA.Identity.Application.Features.Users.Queries;
using ONA.Identity.Presentation.Contracts;

namespace ONA.Identity.Presentation.Controllers;

/// <summary>Each endpoint delegates to exactly one CQRS handler. The caller's id comes from the JWT `sub` claim.</summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private string CallerId => User.FindFirst("sub")?.Value ?? string.Empty;

    // ---- anonymous ----

    [HttpPost, AllowAnonymous]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Register(
        RegisterUserRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var user = await sender.Send(
            new RegisterUserCommand(request.Email, request.DisplayName, request.Password), cancellationToken);
        return Created($"/api/v1/users/{user.Id}", user);
    }

    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResultDto>> Login(
        LoginRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new AuthenticateUserCommand(request.Email, request.Password), cancellationToken));

    /// <summary>Always 202, whether or not the email is registered (no user enumeration).</summary>
    [HttpPost("password-reset/request"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPasswordReset(
        PasswordResetRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new RequestPasswordResetCommand(request.Email), cancellationToken);
        return Accepted();
    }

    [HttpPost("password-reset/confirm"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmPasswordReset(
        ConfirmPasswordResetRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ConfirmPasswordResetCommand(request.Email, request.Token, request.NewPassword), cancellationToken);
        return NoContent();
    }

    // ---- signed-in user ----

    [HttpGet("me"), Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetMe(
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetUserByIdQuery(CallerId), cancellationToken);
        return user is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found") : Ok(user);
    }

    [HttpPut("me"), Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> UpdateMe(
        UpdateUserRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateUserCommand(CallerId, request.DisplayName), cancellationToken));

    [HttpPost("me/password"), Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMyPassword(
        ChangePasswordRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ChangePasswordCommand(CallerId, request.CurrentPassword, request.NewPassword), cancellationToken);
        return NoContent();
    }

    // ---- administrators ----

    [HttpGet, Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAllUsersQuery(), cancellationToken));

    [HttpGet("{userId}"), Authorize(Roles = "Admin")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(
        string userId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetUserByIdQuery(userId), cancellationToken);
        return user is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found") : Ok(user);
    }

    [HttpPut("{userId}/role"), Authorize(Roles = "Admin")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> ChangeRole(
        string userId,
        ChangeRoleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ChangeUserRoleCommand(CallerId, userId, request.Role), cancellationToken));
}
