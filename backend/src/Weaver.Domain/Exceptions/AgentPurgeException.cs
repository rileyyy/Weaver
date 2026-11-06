namespace Weaver.Domain.Exceptions;

/// <summary>
/// An agent account tried to purge the Trash. Read-write agents can delete and restore, but
/// purging can't be undone, so it's for humans only, whatever the key's scope (ADR-0007).
/// </summary>
public class AgentPurgeException : DomainException
{
    public AgentPurgeException()
        : base(
            DomainErrorKind.Forbidden,
            "Agent accounts can't purge the Trash, whatever their key's scope. Ask a human to purge it.")
    {
    }
}
