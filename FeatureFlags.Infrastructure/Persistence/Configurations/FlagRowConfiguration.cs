using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FeatureFlags.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the flags read model onto the same <c>feature_flags</c>/<c>feature_flag_states</c> tables
/// the write side used to own outright — same shape, same names, same indexes, just no longer the
/// target of EF's own change tracking for a flag's business rules.
/// </summary>
internal sealed class FlagRowConfiguration : IEntityTypeConfiguration<FlagRow>
{
    /// <summary>
    /// Named explicitly (rather than left to EF's convention) because FeatureFlagRepository
    /// matches on it to turn a unique violation into a duplicate-key failure.
    /// </summary>
    internal const string KeyIndexName = "IX_feature_flags_Key";

    internal const string StatesTableName = "feature_flag_states";

    public void Configure(EntityTypeBuilder<FlagRow> builder)
    {
        builder.ToTable("feature_flags");

        builder.HasKey(row => row.Id);

        // Ids come from Guid.CreateVersion7() in the domain factory, not from the database.
        builder.Property(row => row.Id)
            .ValueGeneratedNever();

        builder.Property(row => row.Key)
            .HasConversion(
                key => key.Value,
                value => FlagKey.FromPersisted(value))
            .HasMaxLength(FlagKey.MaxLength)
            .IsRequired();

        builder.HasIndex(row => row.Key)
            .HasDatabaseName(KeyIndexName)
            .IsUnique();

        builder.Property(row => row.Name)
            .HasMaxLength(FeatureFlag.MaxNameLength)
            .IsRequired();

        builder.Property(row => row.Description)
            .HasMaxLength(FeatureFlag.MaxDescriptionLength)
            .IsRequired();

        builder.Property(row => row.CreatedAt)
            .IsRequired();

        builder.Property(row => row.UpdatedAt)
            .IsRequired();

        // Owned rather than related: a state has no identity away from its flag, so it is loaded
        // with the flag, saved with it, and deleted with it — no Include, no separate repository.
        builder.OwnsMany(row => row.States, state =>
        {
            state.ToTable(StatesTableName);

            state.WithOwner().HasForeignKey("FlagId");

            // The composite key is the "one state per environment per flag" rule. Stating it as the
            // key rather than a unique index means the database cannot hold a second one at all.
            state.HasKey("FlagId", nameof(FlagStateRow.Environment));

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
    }
}
