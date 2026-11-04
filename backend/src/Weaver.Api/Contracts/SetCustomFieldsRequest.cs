using System.Text.Json;

namespace Weaver.Api.Contracts;

/// <summary>
/// Custom field values by field id. A field left out keeps its value; <c>null</c> clears it.
/// </summary>
public record SetCustomFieldsRequest(IReadOnlyDictionary<Guid, JsonElement> Values, uint? ExpectedVersion = null);
