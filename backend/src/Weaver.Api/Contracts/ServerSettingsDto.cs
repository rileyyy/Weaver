using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record ServerSettingsDto(string TimeZoneId, int TrashRetentionDays)
{
    public static ServerSettingsDto FromEntity(ServerSettings settings) => new(settings.TimeZoneId, settings.TrashRetentionDays);
}
