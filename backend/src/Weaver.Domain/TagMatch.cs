namespace Weaver.Domain;

/// <summary>How the work-item query, or a View, with several tags combines them.</summary>
public enum TagMatch
{
    /// <summary>An item with at least one of the tags matches.</summary>
    Any,

    /// <summary>Only an item with every one of the tags matches.</summary>
    All,
}
