namespace Weaver.Api.Contracts;

/// <summary>How many days a Space's Board keeps showing a Done item after its completion date, 1 to 3650.</summary>
public record SetDoneHidingPeriodRequest(int Days);
