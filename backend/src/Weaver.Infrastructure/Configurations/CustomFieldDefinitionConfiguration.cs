using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFields");

        builder.Property(f => f.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Options).HasJsonConversion();
        builder.Ignore(f => f.IsSelect);
        builder.Ignore(f => f.ChangeName);

        // Cascade: a Type's fields mean nothing without it. Deleting a Type first moves its
        // items off it, which clears their values (WorkItem.ChangeType).
        builder.HasOne<WorkItemType>()
            .WithMany()
            .HasForeignKey(f => f.TypeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(f => new { f.TypeId, f.Order }).IsUnique();
    }
}
