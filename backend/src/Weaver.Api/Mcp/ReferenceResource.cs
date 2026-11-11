using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Weaver.Api.Mcp;

[McpServerResourceType]
public static class ReferenceResource
{
    public const string Uri = "weaver://reference";

    [McpServerResource(UriTemplate = Uri, Name = "weaver_reference", Title = "Weaver reference", MimeType = "text/markdown")]
    [Description("Weaver's vocabulary, what every tool is for, and step-by-step workflows (finding items by " +
        "Number, resolving When phrases, reporting dependency conflicts, handling write conflicts).")]
    public static string Read() => AgentGuidance.Reference;
}
