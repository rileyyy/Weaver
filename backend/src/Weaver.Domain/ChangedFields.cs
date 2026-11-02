namespace Weaver.Domain;

/// <summary>
/// Collects which fields a write actually changed, so setting a field to its current value
/// isn't reported as a change.
/// </summary>
internal sealed class ChangedFields
{
    private readonly List<string> _names = [];

    public IReadOnlyList<string> Names => _names;

    /// <summary>The patched value, or <paramref name="current"/> when the patch leaves it out.</summary>
    public T Apply<T>(string field, T current, Optional<T> patch) =>
        patch.HasValue ? Set(field, current, patch.Value) : current;

    public T Set<T>(string field, T current, T next)
    {
        if (!EqualityComparer<T>.Default.Equals(current, next))
        {
            _names.Add(field);
        }

        return next;
    }
}
