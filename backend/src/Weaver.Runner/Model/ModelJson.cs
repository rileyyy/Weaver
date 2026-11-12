using System.Text.Json;

namespace Weaver.Runner.Model;

/// <summary>Reading the JSON object a model was asked to answer with.</summary>
public static class ModelJson
{
    /// <summary>
    /// Small models often wrap JSON in a code fence or a sentence despite being told not to, so the
    /// outermost braces are read rather than the whole text.
    /// </summary>
    public static bool TryReadObject(string raw, out JsonElement root)
    {
        root = default;
        var open = raw.IndexOf('{');
        var close = raw.LastIndexOf('}');
        if (open < 0 || close < open)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw[open..(close + 1)]);
            root = document.RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
