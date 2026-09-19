using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class UserTools
{
    private readonly IUserService _users;

    public UserTools(IUserService users)
    {
        _users = users;
    }

    [McpServerTool(Name = "list_users", ReadOnly = true)]
    [Description("Lists every user, e.g. to find an id for assigning a work item.")]
    public async Task<IReadOnlyList<UserDto>> ListUsers(CancellationToken ct)
    {
        var users = await _users.ListAsync(ct);
        return users.Select(UserDto.FromEntity).ToList();
    }
}
