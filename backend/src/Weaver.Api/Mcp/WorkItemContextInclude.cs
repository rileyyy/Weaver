namespace Weaver.Api.Mcp;

/// <summary>What <c>get_work_item_context</c> can add around the item, one flag per <c>include</c> value.</summary>
[Flags]
public enum WorkItemContextInclude
{
    None = 0,
    Ancestors = 1 << 0,
    Children = 1 << 1,
    Comments = 1 << 2,
    Links = 1 << 3,
    History = 1 << 4,
    Attachments = 1 << 5,
}
