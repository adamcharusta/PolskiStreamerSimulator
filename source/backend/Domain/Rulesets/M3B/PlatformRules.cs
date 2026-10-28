using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>What a move to <see cref="To"/> would keep: the retention and follower range, and whether this use executes the move.</summary>
public sealed record MigrationEstimate(
    Platform From,
    Platform To,
    bool Executes,
    Fixed RetentionMin,
    Fixed RetentionMax,
    long FollowersMin,
    long FollowersMax,
    PlatformProfile Target);

/// <summary>M3-B: the platform the career streams on, what it changes in the week's balance, and moves between platforms.</summary>
public static class PlatformRules
{
    /// <summary>The ID of the ruleset-built decision that opens when the current platform is banned.</summary>
    public const string BanDecisionId = "platform-ban";

    private const string MigrationDetail = "migration";
    private const string MigrationLineCode = "followers:migration";

    /// <summary>
    /// The balance with the current platform's factors in place of the scalar platform values (reach times the
    /// category's reach on that platform); <paramref name="b"/> itself when the balance has no platforms.
    /// </summary>
    public static BalanceConstants Effective(BalanceConstants b, CareerState s, Category category)
    {
        if (b.Platforms is not { } platforms)
        {
            return b;
        }

        var profile = platforms[Economy.Current(s)];
        return b with
        {
            PlatformReach = profile.Reach * profile.CategoryReach[category],
            PlatformSubYield = profile.SubYield,
            PlatformAdYield = profile.AdYield,
        };
    }

    /// <summary>The platforms the career could move to: neither banned nor the current one, in enum order.</summary>
    public static ImmutableArray<Platform> Accessible(CareerState s)
        => [.. Enum.GetValues<Platform>().Where(p => p != Economy.Current(s) && !Economy.IsBanned(s, p))];

    /// <summary>True when the current platform is banned and another platform is still open.</summary>
    public static bool MustMove(CareerState s)
        => Economy.IsBanned(s, Economy.Current(s)) && !Accessible(s).IsEmpty;

    /// <summary>The flag a first <see cref="MainAction.PlanPlatformMigration"/> toward <paramref name="target"/> sets.</summary>
    public static string AnnouncedFlag(Platform target) => $"migration-announced:{target}";

    /// <summary>
    /// <c>clamp(min, max, base + credibility / credibility divisor − drama / drama divisor + announced bonus − ban penalty)</c>:
    /// the share of followers that follows a move to <paramref name="target"/>.
    /// </summary>
    public static Fixed Retention(CareerState s, Platform target, bool forced, MigrationBalance m)
    {
        var r = m.RetentionBase
            + (s.Credibility / m.CredibilityDivisor)
            - (s.Drama / m.DramaDivisor)
            + (CareerFlags.IsActive(s, AnnouncedFlag(target)) ? m.AnnouncedBonus : Fixed.Zero)
            - (forced ? m.BanPenalty : Fixed.Zero);
        return r.Clamp(m.RetentionMin, m.RetentionMax);
    }

    /// <summary>The retention range (± jitter, clamped) and follower range of a move; it executes when forced or announced.</summary>
    public static MigrationEstimate Estimate(CareerState s, ContentSnapshot c, Platform target, bool forced)
    {
        var m = c.Balance.Migration!;
        var r = Retention(s, target, forced, m);
        var min = (r - m.Jitter).Clamp(m.RetentionMin, m.RetentionMax);
        var max = (r + m.Jitter).Clamp(m.RetentionMin, m.RetentionMax);
        return new MigrationEstimate(
            Economy.Current(s),
            target,
            forced || CareerFlags.IsActive(s, AnnouncedFlag(target)),
            min,
            max,
            Kept(s.Followers, min),
            Kept(s.Followers, max),
            c.Balance.Platforms![target]);
    }

    /// <summary>The estimate of a migration plan; null for any other plan or without the platform sections.</summary>
    public static MigrationEstimate? Preview(CareerState s, ContentSnapshot c, WeekPlan plan)
        => plan is { Action: MainAction.PlanPlatformMigration, TargetPlatform: { } target } && c.Balance is { Platforms: not null, Migration: not null }
            ? Estimate(s, c, target, forced: false)
            : null;

    /// <summary>A drawn retention: uniform in <c>retention ± jitter</c>, clamped to the retention bounds.</summary>
    public static Fixed Draw(Fixed retention, MigrationBalance m, ref Pcg32 rng)
    {
        (var drawn, rng) = rng.NextFixed(retention - m.Jitter, retention + m.Jitter);
        return drawn.Clamp(m.RetentionMin, m.RetentionMax);
    }

    /// <summary>
    /// A migration plan at the start of the week. Not announced: announces the move (flag for
    /// <see cref="MigrationBalance.AnnounceWeeks"/> weeks, credibility change with detail <c>migration</c>). Announced:
    /// draws the retention from the state before the week, clears the flag and freezes the move in the pending week;
    /// it executes at settlement.
    /// </summary>
    public static CareerState Plan(CareerState before, CareerState s, Platform target, MigrationBalance m, ref Pcg32 rng)
    {
        var pending = s.Pending!;
        var flag = AnnouncedFlag(target);
        if (!CareerFlags.IsActive(before, flag))
        {
            var announced = Stats.Add(CareerFlags.Set(s, flag, m.AnnounceWeeks), StatKey.Credibility, m.AnnounceCredibility, pending.Plan.Category);
            return announced with { Pending = pending with { Lines = pending.Lines.AddRange(Stats.Changes(s, announced, MigrationDetail)) } };
        }

        var retention = Draw(Retention(before, target, forced: false, m), m, ref rng);
        return CareerFlags.Clear(s, flag) with { Pending = pending with { Migration = new Migration(Economy.Current(before), target, retention, Forced: false) } };
    }

    /// <summary>
    /// Executes a frozen move after the week's audience change: keeps <c>round(followers × retention)</c> (the rest
    /// counts as lost this week and as former followers), reports <c>followers:migration</c>, switches the platform
    /// (a forced move has switched already) and ends the contracts tied to other platforms.
    /// </summary>
    public static (SettledCore Core, MigrationResult? Result) ApplyMigration(SettledCore core, Migration? migration, ContentSnapshot c, int settledWeek)
    {
        if (migration is null)
        {
            return (core, null);
        }

        var before = core.State.Followers;
        var after = Kept(before, migration.Retention);
        var lost = before - after;
        var state = core.State with { Followers = after, FormerFollowers = (core.State.FormerFollowers ?? 0) + lost };
        if (!migration.Forced)
        {
            state = state with { Platform = migration.To, PlatformSinceWeek = settledWeek + 1 };
        }

        var moved = core with
        {
            State = EndTiedContracts(state, c, migration.To),
            Audience = core.Audience with { Lost = core.Audience.Lost + lost, Net = core.Audience.Net - lost },
            StatLines = core.StatLines.Add(new ReportLine(MigrationLineCode, Delta: migration.Retention, Detail: migration.To.ToString())),
        };
        return (moved, new MigrationResult(migration.From, migration.To, migration.Retention, before, after));
    }

    /// <summary>Ends, without a penalty, every deal whose template lists platforms that do not include <paramref name="to"/>.</summary>
    public static CareerState EndTiedContracts(CareerState s, ContentSnapshot c, Platform to)
        => s.Contracts is { } deals
            ? s with { Contracts = [.. deals.Where(d => Fits(SponsorRules.Template(c, d.TemplateId), to))] }
            : s;

    /// <summary>
    /// The ruleset-built crisis decision after a ban: title and text from <see cref="BalanceConstants.PlatformBan"/>, one
    /// guaranteed choice <c>move-{Platform}</c> without effects per accessible platform, in enum order.
    /// </summary>
    public static EventDefinition BanDecision(CareerState s, ContentSnapshot c)
    {
        var text = c.Balance.PlatformBan!;
        var choices = Accessible(s)
            .Select(p => new ChoiceDefinition(
                MoveChoice(p), null, text.Choices[p], Guaranteed: true, Fixed.One, [], AllowsCritical: false, new OutcomeSet([], [], null, null)))
            .ToImmutableArray();
        return new EventDefinition(
            BanDecisionId,
            null,
            text.Text,
            Weight: 1,
            CooldownWeeks: 0,
            new Eligibility(CareerWeek.First, CareerWeek.Last, ImmutableDictionary<StatKey, Fixed>.Empty, ImmutableDictionary<StatKey, Fixed>.Empty),
            choices,
            Phase: EventPhase.Crisis,
            Title: text.Title);
    }

    /// <summary>The ban decision's odds with its definition and the forced-move estimate of each choice.</summary>
    public static EventPresentation PresentBan(CareerState s, ContentSnapshot c, EventPresentation presented)
        => presented with
        {
            Definition = BanDecision(s, c),
            Choices = [.. presented.Choices.Select(o => o with { Migration = Estimate(s, c, Target(o.ChoiceId), forced: true) })],
        };

    /// <summary>
    /// Answers the ban decision: draws the forced retention from the state before the move, switches the platform now
    /// (since this week), ends tied contracts, carries the move into the next settlement with a guaranteed roll and pops
    /// the queue. A choice that is not offered is <see cref="DomainErrorCode.UnknownChoice"/>.
    /// </summary>
    public static Result<CareerState> ResolveBan(CareerState s, ContentSnapshot c, string choiceId)
    {
        if (!Accessible(s).Any(p => MoveChoice(p) == choiceId))
        {
            return Result<CareerState>.Fail(DomainErrorCode.UnknownChoice, choiceId);
        }

        var target = Target(choiceId);
        var m = c.Balance.Migration!;
        var rng = s.Rng;
        var retention = Draw(Retention(s, target, forced: true, m), m, ref rng);
        var carry = s.Carry ?? DecisionCarry.Empty;
        var moved = EndTiedContracts(s with { Platform = target, PlatformSinceWeek = s.Week.Value }, c, target);
        return Result<CareerState>.Ok(moved with
        {
            Rng = rng,
            StateVersion = s.StateVersion + 1,
            Carry = carry with
            {
                Migration = new Migration(Economy.Current(s), target, retention, Forced: true),
                Rolls = carry.Rolls.Add(new RollResult(BanDecisionId, choiceId, null, 100, [], RollOutcome.Guaranteed)),
            },
            PendingDecisions = s.PendingDecisions!.Value.RemoveAt(0),
            Metrics = s.Metrics with { EventsResolved = s.Metrics.EventsResolved + 1 },
        });
    }

    private static string MoveChoice(Platform p) => $"move-{p}";

    private static Platform Target(string choiceId) => Enum.Parse<Platform>(choiceId["move-".Length..]);

    private static bool Fits(SponsorTemplate t, Platform to) => t.Platforms.IsEmpty || t.Platforms.Contains(to);

    private static long Kept(long followers, Fixed retention) => (Fixed.FromInt(followers) * retention).RoundToLong();
}
