using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record ServerSettingsDto(string TimeZoneId)
{
    public static ServerSettingsDto FromEntity(ServerSettings settings) => new(settings.TimeZoneId);
}
