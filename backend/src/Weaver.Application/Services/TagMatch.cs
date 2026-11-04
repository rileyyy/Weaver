namespace Weaver.Application.Services;

/// <summary>How a <see cref="WorkItemQuery"/> with several tags combines them.</summary>
public enum TagMatch
{
    /// <summary>An item with at least one of the tags matches.</summary>
    Any,

    /// <summary>Only an item with every one of the tags matches.</summary>
    All,
}
