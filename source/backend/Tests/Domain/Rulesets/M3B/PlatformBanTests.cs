using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class PlatformBanTests
{
    private static CareerState Banned(params Platform[] alsoBanned)
        => M3BPlay.Fresh() with
        {
            Week = new CareerWeek(40),
            LastSettledWeek = 39,
            Followers = 10_000,
            Credibility = Fixed.FromInt(50),
            Drama = Fixed.Zero,
            BannedPlatforms = [Platform.Czatnia, .. alsoBanned],
            PendingDecisions = ["platform-ban"],
        };

    private static bool Between(Fixed value, int minPercent, int maxPercent)
        => value >= Fixed.FromRatio(minPercent, 100) && value <= Fixed.FromRatio(maxPercent, 100);

    [Fact]
    public void Ban_decision_offers_each_accessible_platform_with_its_retention()
    {
        var presented = M3BPlay.Engine.PresentDecision(Banned(), M3BPlay.C).Value;
        Assert.Equal(["move-PulsLive", "move-Nurt"], presented.Choices.Select(o => o.ChoiceId));
        Assert.All(presented.Choices, o => Assert.Equal((true, Fixed.FromRatio(45, 100), Fixed.FromRatio(55, 100)), (o.Guaranteed, o.Migration!.RetentionMin, o.Migration.RetentionMax)));
        Assert.Equal("Ban na platformie", presented.Definition!.Title);
        Assert.Equal("Przenieś się na Nurt", presented.Definition.Choices[1].LabelPl);
    }

    [Fact]
    public void Ban_decision_skips_a_banned_target()
        => Assert.Equal(["move-Nurt"], M3BPlay.Engine.PresentDecision(Banned(Platform.PulsLive), M3BPlay.C).Value.Choices.Select(o => o.ChoiceId));

    [Fact]
    public void Moving_switches_now_and_applies_retention_at_the_next_settlement()
    {
        var moved = M3BPlay.ResolveQueued(Banned(), "move-Nurt");
        Assert.Equal((Platform.Nurt, 40, true), (Economy.Current(moved), moved.PlatformSinceWeek, moved.Carry!.Migration!.Forced));
        Assert.Equal(10_000, moved.Followers);
        Assert.True(moved.PendingDecisions is not { Length: > 0 });
        var (after, report) = M3BPlay.Week(moved, M3BPlay.Plan(MainAction.NoAction));
        Assert.True(Between(report.Migration!.Retention, 45, 55));
        Assert.Contains(report.DecisionRolls, r => r is { EventId: "platform-ban", Outcome: RollOutcome.Guaranteed });
        Assert.Equal(report.Migration.FollowersBefore - report.Migration.FollowersAfter, after.FormerFollowers);
        Assert.Equal(Platform.Nurt, Economy.Current(after));
        Assert.Empty(CareerInvariants.Validate(after));
    }

    [Fact]
    public void Ban_opens_before_a_queued_recovery_crisis()
    {
        var s = M3BPlay.Fresh() with { Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)] };
        var (after, _) = UntilBan(s);
        Assert.Equal(["platform-ban", "burnout-recovery"], after.PendingDecisions!.Value);
    }

    [Fact]
    public void Ban_move_ends_tied_contracts_without_penalty()
    {
        var s = Banned() with { Platform = Platform.PulsLive, BannedPlatforms = [Platform.PulsLive], Contracts = [new SponsorContract("puls-gear:30", "puls-gear", SponsorKind.Contract, 30, 33, 37, 500, 0)] };
        var moved = M3BPlay.ResolveQueued(s, "move-Nurt");
        Assert.Empty(moved.Contracts!.Value);
        Assert.DoesNotContain(M3BPlay.Week(moved, M3BPlay.Plan(MainAction.NoAction)).Report.Costs, l => l.Code == "cost:sponsor-penalty");
    }

    [Fact]
    public void Offer_for_the_banned_platform_is_refused_after_the_move()
    {
        var s = Banned() with { Platform = Platform.PulsLive, BannedPlatforms = [Platform.PulsLive], Offers = [new SponsorOffer("puls-gear:40", "puls-gear", 40, 41, 500)] };
        var moved = M3BPlay.ResolveQueued(s, "move-Nurt");
        Assert.Single(moved.Offers!.Value);
        Assert.Equal("prerequisite:platform", M3BPlay.Engine.RespondOffer(moved, M3BPlay.C, "puls-gear:40", true, moved.StateVersion).Error.Detail);
    }

    [Fact]
    public void Ban_blocks_the_week_until_answered()
    {
        Assert.True(M3BPlay.Engine.RequiresPlayerDecision(Banned(), M3BPlay.C).Value);
        Assert.Equal(DomainErrorCode.DecisionPending, M3BPlay.Engine.StartWeek(Banned(), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction), 0).Error.Code);
    }

    [Fact]
    public void Retire_after_a_ban_move_applies_the_retention()
    {
        var moved = M3BPlay.ResolveQueued(Banned(), "move-Nurt");
        var retired = M3BPlay.Engine.Retire(moved, M3BPlay.C, moved.StateVersion).Value;
        Assert.Equal(10_000 - retired.Followers, retired.FormerFollowers);
        Assert.InRange(retired.Followers, 4_500, 5_500);
    }

    [Fact]
    public void Unknown_move_is_rejected()
        => Assert.Equal(DomainErrorCode.UnknownChoice, M3BPlay.Engine.ResolveDecision(Banned(Platform.PulsLive), M3BPlay.C, "move-PulsLive", 0).Error.Code);

    /// <summary>
    /// With burnout at 100 and energy equal to the stream cost (so the recovery crisis is due after the week) and 9,000 PLN,
    /// the first seed in 1–400 whose Gambling <c>High</c> no-action week bans the platform.
    /// </summary>
    private static (CareerState State, WeekReport Report) UntilBan(CareerState s)
    {
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var seeded = s with { CashPln = 9_000, Energy = M3BPlay.C.Balance.StreamEnergyCost, Burnout = Fixed.Hundred, Rng = M3BPlay.Fresh(seed).Rng };
            var (after, report) = M3BPlay.Week(seeded, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.High));
            if (report.Strike is { Banned: true } && (after.Energy == Fixed.Zero || after.Burnout == Fixed.Hundred))
            {
                return (after, report);
            }
        }

        throw new InvalidOperationException("No ban in 400 seeds.");
    }
}
