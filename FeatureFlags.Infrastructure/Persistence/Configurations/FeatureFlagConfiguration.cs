using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FeatureFlags.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    /// <summary>
    /// Named explicitly (rather than left to EF's convention) because FeatureFlagRepository
    /// matches on it to turn a unique violation into a duplicate-key failure.
    /// </summary>
    internal const string KeyIndexName = "IX_feature_flags_Key";

    /// <summary>Shared with the AddFlagStates migration, which backfills this table by hand.</summary>
    internal const string StatesTableName = "feature_flag_states";

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

        builder.Property(flag => flag.CreatedAt)
            .IsRequired();

        builder.Property(flag => flag.UpdatedAt)
            .IsRequired();

        // Owned rather than related: a state has no identity away from its flag, so it is loaded
        // with the flag, saved with it, and deleted with it — no Include, no separate repository.
        builder.OwnsMany(flag => flag.States, state =>
        {
            state.ToTable(StatesTableName);

            state.WithOwner().HasForeignKey("FlagId");

            // The composite key is the "one state per environment per flag" rule. Stating it as the
            // key rather than a unique index means the database cannot hold a second one at all.
            state.HasKey("FlagId", nameof(FlagState.Environment));

            state.Property(candidate => candidate.Environment)
                .HasColumnName("Environment")
                .HasConversion(
                    environment => environment.Value,
                    value => EnvironmentKey.FromPersisted(value))
                .HasMaxLength(EnvironmentKey.MaxLength)
                .IsRequired();

            state.Property(candidate => candidate.IsEnabled)
                .IsRequired();

            state.Property(candidate => candidate.UpdatedAt)
                .IsRequired();
        });

        builder.Navigation(flag => flag.States)
            .HasField("_states")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
