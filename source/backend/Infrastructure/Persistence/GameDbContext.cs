using Microsoft.EntityFrameworkCore;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>EF Core context for the game tables in the <c>game</c> schema.</summary>
public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public const string Schema = "game";

    public DbSet<CareerRow> Careers => Set<CareerRow>();

    public DbSet<CareerSecretRow> CareerSecrets => Set<CareerSecretRow>();

    public DbSet<CareerActionRow> CareerActions => Set<CareerActionRow>();

    public DbSet<ContentSnapshotRow> ContentSnapshots => Set<ContentSnapshotRow>();

    public DbSet<SeasonRow> Seasons => Set<SeasonRow>();

    public DbSet<SeasonExtensionRow> SeasonExtensions => Set<SeasonExtensionRow>();

    public DbSet<LeaderboardEntryRow> LeaderboardEntries => Set<LeaderboardEntryRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ContentSnapshotRow>(e =>
        {
            e.ToTable("content_snapshots");
            e.HasKey(x => x.Digest);
            e.Property(x => x.Digest).HasColumnName("digest").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.BalanceJson).HasColumnName("balance_json");
            e.Property(x => x.EventsJson).HasColumnName("events_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<CareerRow>(e =>
        {
            e.ToTable("careers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(20);
            e.Property(x => x.RulesetId).HasColumnName("ruleset_id").HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ContentDigest).HasColumnName("content_digest").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.Ranked).HasColumnName("ranked");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            e.Property(x => x.StateVersion).HasColumnName("state_version").IsConcurrencyToken();
            e.Property(x => x.StateJson).HasColumnName("state_json");
            e.Property(x => x.StateHash).HasColumnName("state_hash").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastActionAt).HasColumnName("last_action_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.SeasonId).HasColumnName("season_id");
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_careers_expires_at");
            e.HasOne<ContentSnapshotRow>().WithMany().HasForeignKey(x => x.ContentDigest).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SeasonRow>().WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SeasonRow>(e =>
        {
            e.ToTable("seasons");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.RulesetId).HasColumnName("ruleset_id").HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ContentDigest).HasColumnName("content_digest").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");

            // One open season: a concurrent rollover loses here and reads the winner.
            e.HasIndex(x => x.ClosedAt).IsUnique().HasFilter("[closed_at] IS NULL").HasDatabaseName("ux_seasons_open");
            e.HasOne<ContentSnapshotRow>().WithMany().HasForeignKey(x => x.ContentDigest).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SeasonExtensionRow>(e =>
        {
            e.ToTable("season_extensions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.SeasonId).HasColumnName("season_id");
            e.Property(x => x.Deadline).HasColumnName("deadline");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            e.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.SeasonId).HasDatabaseName("ix_season_extensions_season");
            e.HasOne<SeasonRow>().WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaderboardEntryRow>(e =>
        {
            e.ToTable("leaderboard_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.SeasonId).HasColumnName("season_id");
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(5);
            e.Property(x => x.Score).HasColumnName("score");
            e.Property(x => x.WeeksPlayed).HasColumnName("weeks_played");
            e.Property(x => x.Difficulty).HasColumnName("difficulty").HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            e.Property(x => x.Ending).HasColumnName("ending").HasConversion<string>().HasMaxLength(32).IsUnicode(false);
            e.Property(x => x.RulesetId).HasColumnName("ruleset_id").HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.FinalStateVersion).HasColumnName("final_state_version");
            e.Property(x => x.FinalStateHash).HasColumnName("final_state_hash").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            e.Property(x => x.HiddenAt).HasColumnName("hidden_at");

            // No foreign key to careers: the retention sweep deletes careers and leaves their results (spec M4-B §6).
            e.HasIndex(x => x.CareerId).IsUnique().HasDatabaseName("ux_leaderboard_entries_career");
            e.HasIndex(x => new { x.SeasonId, x.Score, x.WeeksPlayed, x.SubmittedAt, x.CareerId })
                .IsDescending(false, true, false, false, false)
                .HasFilter("[hidden_at] IS NULL")
                .HasDatabaseName("ix_leaderboard_entries_order");
            e.HasOne<SeasonRow>().WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CareerSecretRow>(e =>
        {
            e.ToTable("career_secrets");
            e.HasKey(x => x.CareerId);
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.RecoveryHash).HasColumnName("recovery_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.CreationHash).HasColumnName("creation_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.CreationRequestHash).HasColumnName("creation_request_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.RotatedAt).HasColumnName("rotated_at");
            e.HasIndex(x => x.CreationHash).IsUnique().HasFilter("[creation_hash] IS NOT NULL").HasDatabaseName("ux_career_secrets_creation");
            e.HasOne<CareerRow>().WithOne().HasForeignKey<CareerSecretRow>(x => x.CareerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerActionRow>(e =>
        {
            e.ToTable("career_actions");
            e.HasKey(x => new { x.CareerId, x.Sequence });
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.Sequence).HasColumnName("sequence").ValueGeneratedNever();
            e.Property(x => x.CommandId).HasColumnName("command_id");
            e.Property(x => x.Step).HasColumnName("step");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            e.Property(x => x.RequestJson).HasColumnName("request_json");
            e.Property(x => x.ResponseJson).HasColumnName("response_json");
            e.Property(x => x.ReportJson).HasColumnName("report_json");
            e.Property(x => x.StateVersionAfter).HasColumnName("state_version_after");
            e.Property(x => x.StateHashAfter).HasColumnName("state_hash_after").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.CareerId, x.CommandId, x.Step }).IsUnique().HasDatabaseName("ux_career_actions_command");
            e.HasOne<CareerRow>().WithMany().HasForeignKey(x => x.CareerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
