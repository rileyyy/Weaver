namespace Weaver.Api.IntegrationTests;

/// <summary>The statuses every migrated database starts with (see <c>StatusConfiguration</c>).</summary>
public static class SeededStatuses
{
    public static readonly Guid ToDo = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Doing = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
