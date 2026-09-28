using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Account management for administrators. <c>/api/users</c> stays the everyone-can-read list
/// of active users for pickers; this one includes deactivated accounts and their state.
/// </summary>
[ApiController]
[Route("api/admin/users")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAdministrationService _users;
    private readonly TimeProvider _clock;

    public AdminUsersController(IUserAdministrationService users, TimeProvider clock)
    {
        _users = users;
        _clock = clock;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetAll()
    {
        var users = await _users.ListAsync(User.GetUserId());
        return Ok(users.Select(ToDto));
    }

    /// <summary>A user has no admin view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminUserDto>> Create(CreateUserRequest request)
    {
        var user = await _users.CreateAsync(User.GetUserId(), request.Username, request.Password, request.IsAdmin);
        return CreatedAtAction(nameof(GetAll), ToDto(user));
    }

    [HttpPut("{id:guid}/username")]
    public async Task<ActionResult<AdminUserDto>> Rename(Guid id, RenameUserRequest request) =>
        Ok(ToDto(await _users.RenameAsync(User.GetUserId(), id, request.Username)));

    [HttpPut("{id:guid}/password")]
    public async Task<ActionResult<AdminUserDto>> ResetPassword(Guid id, ResetPasswordRequest request) =>
        Ok(ToDto(await _users.ResetPasswordAsync(User.GetUserId(), id, request.Password)));

    [HttpDelete("{id:guid}/lockout")]
    public async Task<ActionResult<AdminUserDto>> Unlock(Guid id) =>
        Ok(ToDto(await _users.UnlockAsync(User.GetUserId(), id)));

    [HttpPut("{id:guid}/admin")]
    public async Task<ActionResult<AdminUserDto>> SetAdmin(Guid id, SetUserAdminRequest request) =>
        Ok(ToDto(await _users.SetAdminAsync(User.GetUserId(), id, request.IsAdmin)));

    [HttpPut("{id:guid}/active")]
    public async Task<ActionResult<AdminUserDto>> SetActive(Guid id, SetUserActiveRequest request) =>
        Ok(ToDto(await _users.SetActiveAsync(User.GetUserId(), id, request.IsActive)));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _users.DeleteAsync(User.GetUserId(), id);
        return NoContent();
    }

    private AdminUserDto ToDto(Domain.User user) => AdminUserDto.FromEntity(user, _clock.GetUtcNow());
}
