using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>A date left out of the JSON is unchanged; <c>null</c> clears it.</summary>
public record RescheduleWorkItemRequest(
    Optional<DateOnly?> StartDate,
    Optional<DateOnly?> EndDate,
    uint? ExpectedVersion = null);
