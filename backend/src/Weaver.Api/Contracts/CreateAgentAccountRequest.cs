namespace Weaver.Api.Contracts;

public record CreateAgentAccountRequest(string Name, string? Description, string? Handle = null);
