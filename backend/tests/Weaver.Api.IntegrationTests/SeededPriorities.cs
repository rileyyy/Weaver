namespace Weaver.Api.IntegrationTests;

/// <summary>The priorities every migrated database starts with (see <c>WorkItemPriorityConfiguration</c>).</summary>
public static class SeededPriorities
{
    public static readonly Guid Low = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid High = Guid.Parse("00000000-0000-0000-0000-000000000203");
}
