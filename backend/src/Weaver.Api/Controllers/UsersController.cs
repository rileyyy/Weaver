using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Read-only listing of registered users, for pickers such as the
/// work-item assignee field. Account management itself lives under
/// <c>/api/auth</c>.
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll()
    {
        var users = await _users.ListAsync();
        return Ok(users.Select(UserDto.FromEntity));
    }
}
