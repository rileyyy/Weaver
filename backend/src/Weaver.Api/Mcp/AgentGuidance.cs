namespace Weaver.Api.Mcp;

/// <summary>
/// What the MCP server tells agents about working in Weaver: short instructions sent when a
/// client connects, and the longer reference served by <see cref="ReferenceResource"/>.
/// Both are Markdown files embedded in this assembly, so they read and diff as documents.
/// </summary>
public static class AgentGuidance
{
    private static readonly Lazy<string> ServerInstructionsText = new(() => ReadEmbedded("ServerInstructions.md"));
    private static readonly Lazy<string> ReferenceText = new(() => ReadEmbedded("Reference.md"));

    public static string ServerInstructions => ServerInstructionsText.Value;

    public static string Reference => ReferenceText.Value;

    private static string ReadEmbedded(string fileName)
    {
        var name = $"{typeof(AgentGuidance).Namespace}.{fileName}";
        using var stream = typeof(AgentGuidance).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
