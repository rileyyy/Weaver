using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class ServerSettingsConfiguration : IEntityTypeConfiguration<ServerSettings>
{
    public void Configure(EntityTypeBuilder<ServerSettings> builder)
    {
        // The check keeps the table to one row, so reading the settings never has to choose.
        builder.ToTable(t => t.HasCheckConstraint("CK_ServerSettings_Singleton", $"\"Id\" = {ServerSettings.SingletonId}"));
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TimeZoneId).IsRequired().HasMaxLength(ServerSettings.TimeZoneIdMaxLength);

        // Seeded so the row always exists and services only ever update it.
        builder.HasData(ServerSettings.CreateDefault());
    }
}
