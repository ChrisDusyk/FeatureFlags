using FeatureFlags.Domain.Flags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FeatureFlags.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    /// <summary>
    /// Named explicitly (rather than left to EF's convention) because FeatureFlagRepository
    /// matches on it to turn a unique violation into a duplicate-key failure.
    /// </summary>
    internal const string KeyIndexName = "IX_feature_flags_Key";

    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");

        builder.HasKey(flag => flag.Id);

        // Ids come from Guid.CreateVersion7() in the domain factory, not from the database.
        builder.Property(flag => flag.Id)
            .ValueGeneratedNever();

        builder.Property(flag => flag.Key)
            .HasConversion(
                key => key.Value,
                value => FlagKey.FromPersisted(value))
            .HasMaxLength(FlagKey.MaxLength)
            .IsRequired();

        builder.HasIndex(flag => flag.Key)
            .HasDatabaseName(KeyIndexName)
            .IsUnique();

        builder.Property(flag => flag.Name)
            .HasMaxLength(FeatureFlag.MaxNameLength)
            .IsRequired();

        builder.Property(flag => flag.Description)
            .HasMaxLength(FeatureFlag.MaxDescriptionLength)
            .IsRequired();

        builder.Property(flag => flag.IsEnabled)
            .IsRequired();

        builder.Property(flag => flag.CreatedAt)
            .IsRequired();

        builder.Property(flag => flag.UpdatedAt)
            .IsRequired();
    }
}
