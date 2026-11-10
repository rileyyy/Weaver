namespace Weaver.Domain;

/// <summary>The fields a <see cref="SpaceHistoryEntry"/> records. Each says what its values hold.</summary>
public static class SpaceHistoryField
{
    /// <summary>The Space was created. New value: its name.</summary>
    public const string Created = "created";

    /// <summary>The Space was renamed. Values: the name.</summary>
    public const string Name = "name";

    /// <summary>The Space's done-hiding period changed. Values: the number of days.</summary>
    public const string DoneHidingDays = "doneHidingDays";
}
