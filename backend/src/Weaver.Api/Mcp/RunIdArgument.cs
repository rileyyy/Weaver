namespace Weaver.Api.Mcp;

/// <summary>The optional run id argument every tool that changes a work item takes, beside the actor label.</summary>
internal static class RunIdArgument
{
    public const string Description =
        "Optional. The id of the run this change is part of: send the same one on every write of one " +
        "unattended run (e.g. a GUID made at its start), at most 100 characters. Admins review each run's " +
        "changes together.";
}
