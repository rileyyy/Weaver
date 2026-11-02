namespace Weaver.Domain.Exceptions;

/// <summary>
/// An agent account reached an admin operation. Agents can read admin data through the
/// ordinary endpoints but never change it, whatever their key's scope (ADR-0004).
/// </summary>
public class AgentAdministrationException : DomainException
{
    public AgentAdministrationException()
        : base(
            DomainErrorKind.Forbidden,
            "Agent accounts can't administer Weaver, whatever their key's scope. Ask a human administrator.")
    {
    }
}
