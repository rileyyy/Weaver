namespace Weaver.Api.Contracts;

/// <summary>An IANA time zone id, e.g. "Europe/Berlin".</summary>
public record SetTimeZoneRequest(string TimeZoneId);
