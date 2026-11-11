namespace Weaver.Domain;

/// <summary>How a <see cref="View"/> first draws its items.</summary>
public enum ViewLayout
{
    Board,
    Hierarchy,
    Roadmap,

    /// <summary>A list of dated items grouped by day, under Overdue and Today first; suits phones.</summary>
    Agenda,
}
