namespace Weaver.Domain;

/// <summary>
/// Roll-ups (ADR-0002): a parent shows, for each number or currency field that rolls up, the
/// sum of that field over its descendants. Computed when read, never stored, so it can't go
/// stale. Each field is summed on its own, by id: a currency field has one currency, so adding
/// up only one field's values never mixes currencies. The Flutter Hierarchy sums its own tree
/// by the same rule (<c>HierarchyRollUps</c>), so a change to the rule here needs making there too.
/// </summary>
public static class RollUp
{
    /// <param name="descendants">The item's live descendants, at any depth and of any Type.</param>
    /// <param name="fields">The definitions of the descendants' fields; those that don't roll up are skipped.</param>
    /// <returns>
    /// A total for each rolled-up field that at least one descendant has a value for, in
    /// <paramref name="fields"/> order.
    /// </returns>
    public static IReadOnlyList<RollUpTotal> Sum(
        IEnumerable<WorkItem> descendants,
        IEnumerable<CustomFieldDefinition> fields)
    {
        var values = descendants.SelectMany(d => d.CustomFields).ToLookup(v => v.Key, v => v.Value);
        return fields
            .Where(f => f.RollsUp && values.Contains(f.Id))
            .Select(f => new RollUpTotal(f, values[f.Id].Sum(v => v.GetDecimal())))
            .ToList();
    }
}
