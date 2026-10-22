using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Careers;

public class CareerInvariantsTests
{
    private static CareerState BannedNurt() => M3Play.Fresh() with
    {
        Platform = Platform.Nurt,
        BannedPlatforms = [Platform.Nurt],
        PendingDecisions = [],
    };

    private static SponsorContract Contract(string id) => new(id, "hype", SponsorKind.Contract, 1, 4, 12, 500, 0);

    /// <summary>Each violation code with a state that breaks only that rule; tests take the code, which serializes.</summary>
    private static readonly Dictionary<string, CareerState> EconomyViolationStates = new()
    {
        ["contract-limit"] = M3Play.Fresh() with { Contracts = [Contract("a"), Contract("b"), Contract("c")] },
        ["offer-duplicate"] = M3Play.Fresh() with { Offers = [new SponsorOffer("x:1", "tea", 1, 2, 100), new SponsorOffer("x:2", "tea", 1, 2, 100)] },
        ["banned-platform-unanswered"] = BannedNurt(),
        ["side-business-quality"] = M3Play.Fresh() with { SideBusiness = new SideBusiness(60, Fixed.FromInt(101)) },
        ["former-followers-negative"] = M3Play.Fresh() with { FormerFollowers = -1 },
    };

    public static TheoryData<string> EconomyViolations() => [.. EconomyViolationStates.Keys];

    [Theory]
    [MemberData(nameof(EconomyViolations))]
    public void Economy_state_violations_are_reported(string code)
        => Assert.Equal([code], CareerInvariants.Validate(EconomyViolationStates[code]));

    [Fact]
    public void Banned_platform_with_the_ban_decision_queued_is_valid()
        => Assert.Empty(CareerInvariants.Validate(BannedNurt() with { PendingDecisions = ["platform-ban"] }));

    [Fact]
    public void Two_contracts_and_distinct_offers_are_valid()
        => Assert.Empty(CareerInvariants.Validate(M3Play.Fresh() with
        {
            Contracts = [Contract("a"), Contract("b")],
            Offers = [new SponsorOffer("x:1", "tea", 1, 2, 100), new SponsorOffer("y:1", "mug", 1, 2, 100)],
        }));

    [Fact]
    public void Decisions_pending_during_a_week_are_reported()
    {
        var s = TestCareers.Dorm(1);
        var pending = new PendingWeek(s.Week, new WeekPlan(MainAction.Rest, Category.Fps), [], [], Fixed.Zero, Fixed.One, 0, []);
        Assert.Empty(CareerInvariants.Validate(s with { PendingDecisions = ["d"] }));
        Assert.Empty(CareerInvariants.Validate(s with { Pending = pending, PendingDecisions = [] }));
        Assert.Equal(["decision-during-week"], CareerInvariants.Validate(s with { Pending = pending, PendingDecisions = ["d"] }));
    }

    [Fact]
    public void Expired_flags_are_reported_but_the_boundary_week_is_still_active()
    {
        var s = TestCareers.Dorm(1) with { LastSettledWeek = 10 };
        var flags = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
        Assert.Empty(CareerInvariants.Validate(s with { Flags = flags.Add("edge", 10) }));
        Assert.Equal(["expired-flag-kept"], CareerInvariants.Validate(s with { Flags = flags.Add("old", 9) }));
    }

    [Fact]
    public void Invariants_report_each_violation()
    {
        var s = TestCareers.Dorm(1) with { Energy = Fixed.FromInt(101), Followers = -1, CashPln = -1_000 };
        Assert.Equal(["stat-range:Energy", "followers-negative", "active-insolvent"], CareerInvariants.Validate(s));
    }

    [Fact]
    public void Default_week_is_out_of_range() => Assert.Equal(["week-range"], CareerInvariants.Validate(TestCareers.Dorm(1) with { Week = default }));

    [Fact]
    public void Category_values_are_range_checked()
    {
        var s = TestCareers.Dorm(1);
        s = s with
        {
            Skill = s.Skill.With(Category.Chess, Fixed.FromInt(-1)),
            Boredom = s.Boredom.With(Category.Moba, new Fixed(100_000_001)),
        };
        Assert.Equal(["stat-range:Skill.Chess", "stat-range:Boredom.Moba"], CareerInvariants.Validate(s));
    }

    [Fact]
    public void Negative_viewers_and_bad_equipment_are_reported()
    {
        var s = TestCareers.Dorm(1);
        s = s with { LastLiveViewers = -5, Equipment = s.Equipment.SetItem(EquipmentSlot.Camera, 5).Remove(EquipmentSlot.Connection) };
        Assert.Equal(["viewers-negative", "equipment-tier:Camera", "equipment-tier:Connection"], CareerInvariants.Validate(s));
    }

    [Fact]
    public void Cash_above_the_insolvency_threshold_is_valid()
        => Assert.Empty(CareerInvariants.Validate(TestCareers.Dorm(1) with { CashPln = -999 }));

    [Fact]
    public void Ended_career_requires_reason_and_score()
    {
        var violations = CareerInvariants.Validate(TestCareers.Dorm(1) with { Status = CareerStatus.Ended });
        Assert.Equal(["ended-without-reason", "ended-without-score"], violations);
    }

    [Fact]
    public void Ended_career_may_be_insolvent_but_not_pending()
    {
        var s = TestCareers.Dorm(1) with
        {
            Status = CareerStatus.Ended,
            Ending = EndingReason.Insolvency,
            FinalScore = ScoreCalculator.Compute(0, -5_000, 0, Difficulty.Normal, Fixed.One, EndingReason.Insolvency, TestContent.Valid().Balance),
            CashPln = -5_000,
        };
        Assert.Empty(CareerInvariants.Validate(s));
    }

    [Fact]
    public void Active_career_must_not_carry_an_ending()
        => Assert.Equal(["active-with-ending"], CareerInvariants.Validate(TestCareers.Dorm(1) with { Ending = EndingReason.Completed }));
}
