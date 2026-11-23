using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Identity.Application.Abstractions.Messaging;
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
        [FromServices] ICommandHandler<RegisterUserCommand, UserDto> handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(
            new RegisterUserCommand(request.Email, request.DisplayName, request.Password), cancellationToken);
        return Created($"/api/v1/users/{user.Id}", user);
    }

    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResultDto>> Login(
        LoginRequest request,
        [FromServices] ICommandHandler<AuthenticateUserCommand, LoginResultDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new AuthenticateUserCommand(request.Email, request.Password), cancellationToken));

    /// <summary>Always 202, whether or not the email is registered (no user enumeration).</summary>
    [HttpPost("password-reset/request"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPasswordReset(
        PasswordResetRequest request,
        [FromServices] ICommandHandler<RequestPasswordResetCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RequestPasswordResetCommand(request.Email), cancellationToken);
        return Accepted();
    }

    [HttpPost("password-reset/confirm"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmPasswordReset(
        ConfirmPasswordResetRequest request,
        [FromServices] ICommandHandler<ConfirmPasswordResetCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new ConfirmPasswordResetCommand(request.Email, request.Token, request.NewPassword), cancellationToken);
        return NoContent();
    }

    // ---- signed-in user ----

    [HttpGet("me"), Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetMe(
        [FromServices] IQueryHandler<GetUserByIdQuery, UserDto?> handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserByIdQuery(CallerId), cancellationToken);
        return user is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found") : Ok(user);
    }

    [HttpPut("me"), Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> UpdateMe(
        UpdateUserRequest request,
        [FromServices] ICommandHandler<UpdateUserCommand, UserDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new UpdateUserCommand(CallerId, request.DisplayName), cancellationToken));

    [HttpPost("me/password"), Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMyPassword(
        ChangePasswordRequest request,
        [FromServices] ICommandHandler<ChangePasswordCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new ChangePasswordCommand(CallerId, request.CurrentPassword, request.NewPassword), cancellationToken);
        return NoContent();
    }

    // ---- administrators ----

    [HttpGet, Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(
        [FromServices] IQueryHandler<GetAllUsersQuery, IReadOnlyList<UserDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetAllUsersQuery(), cancellationToken));

    [HttpGet("{userId}"), Authorize(Roles = "Admin")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(
        string userId,
        [FromServices] IQueryHandler<GetUserByIdQuery, UserDto?> handler,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(new GetUserByIdQuery(userId), cancellationToken);
        return user is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found") : Ok(user);
    }

    [HttpPut("{userId}/role"), Authorize(Roles = "Admin")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> ChangeRole(
        string userId,
        ChangeRoleRequest request,
        [FromServices] ICommandHandler<ChangeUserRoleCommand, UserDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ChangeUserRoleCommand(CallerId, userId, request.Role), cancellationToken));
}
