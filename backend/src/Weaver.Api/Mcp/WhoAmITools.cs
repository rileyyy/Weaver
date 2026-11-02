using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WhoAmITools
{
    private readonly ICallerIdentityService _identity;

    public WhoAmITools(ICallerIdentityService identity)
    {
        _identity = identity;
    }

    [McpServerTool(Name = "whoami", ReadOnly = true)]
    [Description(
        "Says which account this connection is signed in as (its name, and whether it's a Human or an " +
        "Agent), and for an agent which API key it used: the key's label and its scope, ReadOnly or " +
        "ReadWrite. Check it before writing if you're unsure what you may change.")]
    public async Task<WhoAmIDto> WhoAmI(CancellationToken ct) =>
        WhoAmIDto.FromIdentity(await _identity.DescribeAsync(ct));
}
