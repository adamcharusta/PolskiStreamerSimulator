using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class SideBusinessTests
{
    private static CareerState Ready => M3BPlay.Fresh() with { Week = new CareerWeek(52), LastSettledWeek = 51, Followers = 10_000, Energy = Fixed.FromInt(60) };

    [Fact]
    public void First_work_opens_the_business()
    {
        var (after, _) = M3BPlay.Week(Ready, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Equal(52, after.SideBusiness!.OpenedWeek);
        Assert.Equal(Fixed.FromInt(12), after.SideBusiness.Quality);   // +15 at start, −3 decay at settlement
        Assert.True(CareerFlags.IsActive(after, "side-business"));
    }

    [Fact]
    public void Later_work_keeps_the_opening_week()
        => Assert.Equal(40, M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(30)) }, M3BPlay.Plan(MainAction.SideBusinessWork)).State.SideBusiness!.OpenedWeek);

    [Theory]
    [InlineData(10_000, 30, 15)]
    [InlineData(20_000, 100, 100)]
    [InlineData(20_000, 0, 0)]
    public void Income_scales_with_followers_and_quality(long followers, int quality, long income)
        => Assert.Equal(income, SideBusinessRules.Income(followers, Fixed.FromInt(quality), M3BPlay.C.Balance.SideBusiness!));

    [Fact]
    public void Open_business_pays_costs_credibility_and_decays()
    {
        var (after, report) = M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(30)) }, M3BPlay.Plan(MainAction.NoAction));
        Assert.Contains(report.Income, l => l is { Code: "income:side-business", AmountPln: 15 });
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Credibility" && l.Detail == "side-business" && l.Delta == Fixed.FromRatio(-2, 10));
        Assert.Equal(Fixed.FromInt(27), after.SideBusiness!.Quality);
    }

    [Fact]
    public void Dormant_business_pays_and_costs_nothing()
    {
        var (after, report) = M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.Zero) }, M3BPlay.Plan(MainAction.NoAction));
        Assert.DoesNotContain(report.Income, l => l.Code == "income:side-business");
        Assert.DoesNotContain(report.StatChanges, l => l.Detail == "side-business");
        Assert.Equal(Fixed.Zero, after.SideBusiness!.Quality);
    }

    [Fact]
    public void Quality_caps_at_100()
        => Assert.Equal(Fixed.FromInt(97), M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(95)) }, M3BPlay.Plan(MainAction.SideBusinessWork)).State.SideBusiness!.Quality);

    [Fact]
    public void Quality_never_decays_below_zero()
        => Assert.Equal(Fixed.Zero, M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(2)) }, M3BPlay.Plan(MainAction.NoAction)).State.SideBusiness!.Quality);

    [Fact]
    public void Opening_breaches_an_adult_business_incompatible_deal()
    {
        var s = Ready with { Contracts = [new SponsorContract("chess-board:52", "chess-board", SponsorKind.OneOff, 52, 53, 53, 250, 0)] };
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 100, Detail: "chess-board" });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Open_business_stops_adult_incompatible_offers()
        => Assert.DoesNotContain(SponsorRules.Eligible(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(30)) }, M3BPlay.C), t => t.Id == "chess-board");

    [Fact]
    public void Is_open_only_with_quality()
    {
        Assert.False(SideBusinessRules.IsOpen(Ready));
        Assert.False(SideBusinessRules.IsOpen(Ready with { SideBusiness = new SideBusiness(40, Fixed.Zero) }));
        Assert.True(SideBusinessRules.IsOpen(Ready with { SideBusiness = new SideBusiness(40, Fixed.One) }));
    }
}
