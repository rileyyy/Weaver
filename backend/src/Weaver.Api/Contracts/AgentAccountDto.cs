using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record AgentAccountDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public static AgentAccountDto FromEntity(User agent) => new(
        agent.Id,
        agent.Username,
        agent.Description,
        agent.IsActive,
        agent.CreatedAtUtc);
}
