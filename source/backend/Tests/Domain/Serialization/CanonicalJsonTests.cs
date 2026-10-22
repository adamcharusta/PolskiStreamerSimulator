using System.Collections.Immutable;
using System.Text;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Serialization;

public class CanonicalJsonTests
{
    private static PendingWeek BasePending() => new(
        new CareerWeek(3),
        new WeekPlan(MainAction.Rest, Category.Fps),
        [],
        [],
        Fixed.Zero,
        Fixed.One,
        0,
        []);

    [Fact]
    public void Stored_m3a_state_round_trips_without_new_fields()
    {
        var s = M3Play.Fresh();
        var started = M3Play.Start(s, MainAction.NoAction);
        foreach (var state in new[] { s, started })
        {
            var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(state));
            foreach (var name in new[] { "Platform", "PlatformSinceWeek", "BannedPlatforms", "Strikes", "FormerFollowers", "Offers", "Contracts", "SideBusiness", "Wager", "TargetPlatform", "Exposure", "Migration" })
            {
                Assert.DoesNotContain($"\"{name}\"", json, StringComparison.Ordinal);
            }

            Assert.Equal(StateHash.Of(state), StateHash.Of(CanonicalJson.Deserialize(CanonicalJson.Serialize(state))));
        }
    }

    [Fact]
    public void M3b_fields_round_trip()
    {
        var s = M3Play.Fresh() with
        {
            Platform = Platform.PulsLive,
            PlatformSinceWeek = 30,
            BannedPlatforms = [Platform.Czatnia],
            Strikes = [new PolicyStrike(Platform.Czatnia, 12), new PolicyStrike(Platform.PulsLive, 31)],
            FormerFollowers = 4_200,
            Offers = [new SponsorOffer("trusted-tea:31", "trusted-tea", 31, 32, 400)],
            Contracts = [new SponsorContract("hype:20", "hype", SponsorKind.Contract, 20, 23, 31, 500, 1)],
            SideBusiness = new SideBusiness(60, Fixed.FromInt(45)),
        };
        var bytes = CanonicalJson.Serialize(s);
        var restored = CanonicalJson.Deserialize(bytes);
        Assert.Equal(bytes, CanonicalJson.Serialize(restored));
        Assert.Equal(Platform.PulsLive, restored.Platform);
        Assert.Equal(new PolicyStrike(Platform.PulsLive, 31), restored.Strikes!.Value[1]);
        Assert.Equal(Fixed.FromInt(45), restored.SideBusiness!.Quality);
    }

    [Fact]
    public void M3b_plan_and_pending_fields_round_trip_and_are_omitted_when_null()
    {
        var plan = new WeekPlan(MainAction.NoAction, Category.Fps, null, null, WagerTier.High, Platform.Nurt);
        var migration = new Migration(Platform.Czatnia, Platform.Nurt, Fixed.FromInt(60), true);
        var pending = BasePending() with
        {
            Plan = plan,
            Wager = new WagerResult(WagerTier.High, 500, 40, 45, false, -500),
            Exposure = 7,
            Migration = migration,
        };
        var s = M3Play.Fresh() with { Pending = pending, Carry = DecisionCarry.Empty with { Migration = migration } };
        var restored = CanonicalJson.Deserialize(CanonicalJson.Serialize(s));
        Assert.Equal(pending.Plan, restored.Pending!.Plan);
        Assert.Equal(pending.Wager, restored.Pending.Wager);
        Assert.Equal(7, restored.Pending.Exposure);
        Assert.Equal(migration, restored.Pending.Migration);
        Assert.Equal(migration, restored.Carry!.Migration);
        Assert.Equal(CanonicalJson.Serialize(s), CanonicalJson.Serialize(restored));
    }

    [Fact]
    public void Pending_week_extras_round_trip_and_are_omitted_when_null()
    {
        var roll = new RollResult("e1", "c1", 42, 60, [new ModifierLine("skill", Fixed.FromInt(5))], RollOutcome.Success);
        var carry = new DecisionCarry([roll], 150, Fixed.FromRatio(1, 3), [new ReportLine("decision-line", 150)]);
        var full = TestCareers.Dorm(7) with
        {
            Pending = BasePending() with { ActionRoll = roll, FollowersPercent = Fixed.FromInt(2), ViralChance = Fixed.FromRatio(1, 4), Carry = carry },
        };
        var bytes = CanonicalJson.Serialize(full);
        var json = Encoding.UTF8.GetString(bytes);
        foreach (var name in new[] { "ActionRoll", "FollowersPercent", "ViralChance", "Carry" })
        {
            Assert.Contains($"\"{name}\"", json, StringComparison.Ordinal);
        }

        Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
        Assert.Equal(carry.CashPln, CanonicalJson.Deserialize(bytes).Pending!.Carry!.CashPln);

        var bare = Encoding.UTF8.GetString(CanonicalJson.Serialize(TestCareers.Dorm(7) with { Pending = BasePending() }));
        foreach (var name in new[] { "ActionRoll", "FollowersPercent", "ViralChance", "Carry" })
        {
            Assert.DoesNotContain($"\"{name}\"", bare, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Stored_m1_state_round_trips_without_new_fields()
    {
        var s = TestCareers.Dorm(7);
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(s));
        foreach (var name in new[] { "Flags", "Scheduled", "PendingDecisions", "Carry", "UsedOneTime" })
        {
            Assert.DoesNotContain($"\"{name}\"", json, StringComparison.Ordinal);
        }

        Assert.Equal(StateHash.Of(s), StateHash.Of(CanonicalJson.Deserialize(CanonicalJson.Serialize(s))));
    }

    [Fact]
    public void M3_fields_round_trip_in_ordinal_order()
    {
        var s = TestCareers.Dorm(7) with
        {
            Flags = ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal).Add("b", 5).Add("a", 1040),
            Scheduled = [new ScheduledEvent("rivalry-2", 12)],
            PendingDecisions = ["burnout-recovery"],
            UsedOneTime = ImmutableHashSet.Create(StringComparer.Ordinal, "first-week"),
            Carry = DecisionCarry.Empty with { CashPln = -200 },
        };
        var bytes = CanonicalJson.Serialize(s);
        var json = Encoding.UTF8.GetString(bytes);
        Assert.True(json.IndexOf("\"a\":1040", StringComparison.Ordinal) < json.IndexOf("\"b\":5", StringComparison.Ordinal));
        Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
    }

    [Fact]
    public void Roundtrip_is_byte_identical()
    {
        var s = TestCareers.Dorm(9);
        var bytes = CanonicalJson.Serialize(s);
        Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
    }

    [Fact]
    public void Roundtrip_preserves_values()
    {
        var s = TestCareers.Dorm(9) with
        {
            CashPln = -250,
            Drama = Fixed.FromRatio(1, 3),
            EventCooldowns = TestCareers.Dorm(9).EventCooldowns.Add("troll-wave", 12),
            Achievements = TestCareers.Dorm(9).Achievements.Add("followers-1k"),
            Ending = null,
        };
        var back = CanonicalJson.Deserialize(CanonicalJson.Serialize(s));
        Assert.Equal(-250, back.CashPln);
        Assert.Equal(Fixed.FromRatio(1, 3), back.Drama);
        Assert.Equal(12, back.EventCooldowns["troll-wave"]);
        Assert.Contains("followers-1k", back.Achievements);
        Assert.Equal(s.Rng, back.Rng);
        Assert.Equal(s.Week, back.Week);
        Assert.Equal(s.Skill, back.Skill);
        Assert.Equal(s.Equipment[EquipmentSlot.Camera], back.Equipment[EquipmentSlot.Camera]);
    }

    [Fact]
    public void Hash_ignores_nothing_and_is_stable()
    {
        Assert.Equal(StateHash.Of(TestCareers.Dorm(9)), StateHash.Of(TestCareers.Dorm(9)));
        Assert.NotEqual(StateHash.Of(TestCareers.Dorm(9)), StateHash.Of(TestCareers.Dorm(10)));
        Assert.NotEqual(StateHash.Of(TestCareers.Dorm(9)), StateHash.Of(TestCareers.Dorm(9) with { Pronouns = PronounSet.Man }));
    }

    [Fact]
    public void Hash_is_lowercase_sha256_hex()
        => Assert.Matches("^[0-9a-f]{64}$", StateHash.Of(TestCareers.Dorm(1)));

    [Fact]
    public void Dictionary_insertion_order_does_not_change_the_bytes()
    {
        var baseState = TestCareers.Dorm(4);
        var a = baseState with { EventCooldowns = baseState.EventCooldowns.Add("zeta", 3).Add("alpha", 5).Add("mid", 9) };
        var b = baseState with { EventCooldowns = baseState.EventCooldowns.Add("mid", 9).Add("alpha", 5).Add("zeta", 3) };
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(a));
        Assert.Equal(CanonicalJson.Serialize(a), CanonicalJson.Serialize(b));
        Assert.Contains("\"EventCooldowns\":{\"alpha\":5,\"mid\":9,\"zeta\":3}", json);
    }

    [Fact]
    public void Fixed_values_are_raw_integers_and_enums_are_names()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(TestCareers.Dorm(3)));
        Assert.Contains("\"Credibility\":50000000", json);
        Assert.Contains("\"Background\":\"DormRoomNewcomer\"", json);
        Assert.Contains("\"Week\":1", json);
    }

    [Fact]
    public void Json_contains_no_floating_point_literals()
        => Assert.DoesNotMatch(@"\d\.\d", Encoding.UTF8.GetString(CanonicalJson.Serialize(TestCareers.Dorm(3))));
}
