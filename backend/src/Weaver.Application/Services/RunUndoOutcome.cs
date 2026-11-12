namespace Weaver.Application.Services;

/// <summary>What undoing a run did with one of the changes it made.</summary>
public enum RunUndoOutcome
{
    /// <summary>Put back as it was before the run.</summary>
    Undone,

    /// <summary>A comment, which an undo keeps: comments are part of the item's public record.</summary>
    Kept,

    /// <summary>Someone else changed the same thing after the run, so their change is kept.</summary>
    ChangedSince,

    /// <summary>The item is in the Trash or purged, so there's nothing left to put back.</summary>
    ItemGone,

    /// <summary>Putting the date back would start the item after its end, which someone has since moved.</summary>
    InvalidDates,

    /// <summary>A kind of change an undo doesn't put back, such as a status or a move.</summary>
    NotUndoable,
}
