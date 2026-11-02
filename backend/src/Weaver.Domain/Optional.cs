namespace Weaver.Domain;

/// <summary>
/// A value that may be left out, as distinct from one set to <c>null</c>. Patch updates need
/// both: a field left out is unchanged, while <c>null</c> clears it.
/// </summary>
public readonly record struct Optional<T>
{
    private readonly T _value;

    public Optional(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static Optional<T> Unset => default;

    public bool HasValue { get; }

    public T Value => HasValue ? _value : throw new InvalidOperationException("The optional value was left out.");

    public T GetValueOr(T fallback) => HasValue ? _value : fallback;

    public static implicit operator Optional<T>(T value) => new(value);
}
