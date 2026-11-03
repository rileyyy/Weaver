namespace Weaver.Domain;

/// <summary>An inclusive span of calendar dates.</summary>
public readonly record struct DateRange(DateOnly Start, DateOnly End);
