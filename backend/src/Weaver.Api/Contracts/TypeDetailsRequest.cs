namespace Weaver.Api.Contracts;

/// <summary>The body for editing a Type; its Space never changes.</summary>
public record TypeDetailsRequest(string Name, string Color);
