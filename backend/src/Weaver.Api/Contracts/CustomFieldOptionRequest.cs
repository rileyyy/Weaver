namespace Weaver.Api.Contracts;

/// <summary>An option to keep (by <see cref="Id"/>, possibly renamed) or, without an id, to add.</summary>
public record CustomFieldOptionRequest(Guid? Id, string Name);
