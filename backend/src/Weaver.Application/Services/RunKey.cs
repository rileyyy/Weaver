namespace Weaver.Application.Services;

/// <summary>
/// What tells one run from another: the run id, and the agent account and key that sent it. Run
/// ids are free text, so two agents may well send the same one; they are still two runs. The key
/// is never missing because only agents, which always sign in with one, make runs.
/// </summary>
public sealed record RunKey(Guid AccountId, Guid ApiKeyId, string RunId);
