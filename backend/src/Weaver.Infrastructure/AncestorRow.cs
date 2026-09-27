namespace Weaver.Infrastructure;

/// <summary>One row of <see cref="PostgresWorkItemHierarchy"/>'s ancestor walk.</summary>
internal sealed record AncestorRow(Guid Id, bool IsCycle);
