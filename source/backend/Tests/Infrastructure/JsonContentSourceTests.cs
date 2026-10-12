using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public sealed class JsonContentSourceTests : IDisposable
{
    private readonly string tempDirectory = Directory.CreateTempSubdirectory("pss-content-").FullName;

    public void Dispose() => Directory.Delete(tempDirectory, recursive: true);

    private string CopyContent(Func<string, string>? editBalance = null, Func<string, string>? editEvents = null)
    {
        var balance = File.ReadAllText(Path.Combine(TestPaths.ContentM1, "balance.json"));
        var events = File.ReadAllText(Path.Combine(TestPaths.ContentM1, "events.json"));
        File.WriteAllText(Path.Combine(tempDirectory, "balance.json"), editBalance?.Invoke(balance) ?? balance);
        File.WriteAllText(Path.Combine(tempDirectory, "events.json"), editEvents?.Invoke(events) ?? events);
        return tempDirectory;
    }

    [Fact]
    public void Repository_content_is_valid()
    {
        var c = new JsonContentSource(TestPaths.ContentM1).Load();
        Assert.Empty(ContentValidator.Validate(c));
        Assert.Matches("^[0-9a-f]{64}$", c.Digest);
        Assert.Equal(5, c.Events.Length);
        Assert.Equal("m1.1", c.Version);
    }

    [Fact]
    public void Digest_is_stable_for_identical_files()
        => Assert.Equal(new JsonContentSource(TestPaths.ContentM1).Load().Digest, new JsonContentSource(CopyContent()).Load().Digest);

    [Fact]
    public void Digest_changes_when_balance_changes()
    {
        var original = new JsonContentSource(TestPaths.ContentM1).Load();
        var edited = new JsonContentSource(CopyContent(b => b.Replace("\"livingCostPln\": 100", "\"livingCostPln\": 101", StringComparison.Ordinal))).Load();
        Assert.Equal(101, edited.Balance.LivingCostPln);
        Assert.NotEqual(original.Digest, edited.Digest);
    }

    [Fact]
    public void Decimal_values_parse_exactly()
    {
        var b = new JsonContentSource(TestPaths.ContentM1).Load().Balance;
        Assert.Equal(350_000, b.QualitySkill.Raw);
        Assert.Equal(100, b.ChurnBoredomFactor.Raw);
        Assert.Equal(1_100_000, b.DifficultyMultiplier[Difficulty.Normal].Raw);
        Assert.Equal(-1_000, b.InsolvencyThresholdPln);
        Assert.Equal([0L, 600, 1_500, 3_500, 8_000], b.EquipmentPricePln);
    }

    [Fact]
    public void Profiles_are_loaded_for_every_background()
    {
        var c = new JsonContentSource(TestPaths.ContentM1).Load();
        var dorm = c.Profiles[Background.DormRoomNewcomer];
        Assert.Equal(2_000, dorm.CashPln);
        Assert.Equal(Fixed.FromInt(18), dorm.Skill[Category.Fps]);
        Assert.Equal(Fixed.One, dorm.ScoreMultiplier);
        Assert.Equal(Fixed.FromRatio(90, 100), c.Profiles[Background.IndustryInsider].ScoreMultiplier);
        Assert.Equal(2, c.Profiles[Background.IndustryInsider].Equipment[EquipmentSlot.Camera]);
    }

    [Fact]
    public void Choices_modifiers_and_effects_are_loaded()
    {
        var raid = new JsonContentSource(TestPaths.ContentM1).Load().Events.Single(e => e.Id == "chat-raid");
        var welcome = raid.Choices.Single(c => c.Id == "welcome");
        Assert.False(welcome.Guaranteed);
        Assert.Equal(Fixed.FromRatio(55, 100), welcome.BaseChance);
        Assert.Equal(StatKey.Credibility, welcome.Modifiers.Single().Stat);
        Assert.Equal(Fixed.FromRatio(2, 1_000), welcome.Modifiers.Single().PerPoint);
        Assert.True(raid.Choices.Single(c => c.Id == "ignore").Guaranteed);
        Assert.NotEmpty(welcome.Outcomes.Success);
    }

    [Fact]
    public void More_than_six_decimals_are_rejected()
    {
        var dir = CopyContent(b => b.Replace("\"qualitySkill\": 0.35", "\"qualitySkill\": 0.3500001", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => new JsonContentSource(dir).Load());
        Assert.Contains("qualitySkill", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_content_lists_validator_codes()
    {
        var dir = CopyContent(editEvents: e => e.Replace("\"weight\": 3", "\"weight\": 0", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => new JsonContentSource(dir).Load());
        Assert.Contains("weight-nonpositive:chat-raid", error.Message, StringComparison.Ordinal);
    }
}
