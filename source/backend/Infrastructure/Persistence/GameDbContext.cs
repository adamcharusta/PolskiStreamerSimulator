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
            e.HasOne<ContentSnapshotRow>().WithMany().HasForeignKey(x => x.ContentDigest).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CareerSecretRow>(e =>
        {
            e.ToTable("career_secrets");
            e.HasKey(x => x.CareerId);
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
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
