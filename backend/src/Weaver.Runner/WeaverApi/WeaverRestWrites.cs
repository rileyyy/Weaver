namespace Weaver.Runner.WeaverApi;

/// <summary>Writes more than one task kind makes, so each client sends them the same way.</summary>
internal static class WeaverRestWrites
{
    /// <summary>
    /// Sets only the dates given and leaves the other unchanged: a date left out of the body is
    /// unchanged, while null would clear it. Refused if the item has moved on from
    /// <paramref name="expectedVersion"/>.
    /// </summary>
    public static Task SetDatesAsync(
        this WeaverRest rest,
        Guid workItemId,
        DateOnly? start,
        DateOnly? end,
        uint expectedVersion,
        string actorLabel,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object> { ["expectedVersion"] = expectedVersion };
        if (start is { } s)
        {
            body["startDate"] = s;
        }

        if (end is { } e)
        {
            body["endDate"] = e;
        }

        return rest.WriteAsync(HttpMethod.Patch, $"api/work-items/{workItemId}/schedule", body, actorLabel, ct);
    }
}
