namespace Weaver.Api.Contracts;

/// <summary>How many days a delete stays in the Trash before it's purged, 1 to 3650.</summary>
public record SetTrashRetentionRequest(int Days);
