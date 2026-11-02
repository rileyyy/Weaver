using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>Read-only: agents never change admin settings (ADR-0004).</summary>
[McpServerToolType]
public class ServerSettingsTools
{
    private readonly IServerSettingsService _settings;

    public ServerSettingsTools(IServerSettingsService settings)
    {
        _settings = settings;
    }

    [McpServerTool(Name = "get_server_settings", ReadOnly = true)]
    [Description(
        "Gets the server-wide settings. timeZoneId is the IANA time zone (e.g. \"Europe/Berlin\") that " +
        "defines \"today\" for the server, such as when a repeating item's next occurrence is due; " +
        "use it to interpret relative dates. Only an admin can change these settings, in the app.")]
    public async Task<ServerSettingsDto> GetServerSettings(CancellationToken ct) =>
        ServerSettingsDto.FromEntity(await _settings.GetAsync(ct));
}
