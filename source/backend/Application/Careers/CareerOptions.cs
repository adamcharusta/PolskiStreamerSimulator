using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record BackgroundOptionView(
    Background Background,
    long CashPln,
    long Followers,
    decimal Drama,
    decimal Credibility,
    decimal Energy,
    CategoryValuesView Skill,
    EquipmentView Equipment,
    decimal ScoreMultiplier);

public sealed record DifficultyOptionView(Difficulty Difficulty, decimal ScoreMultiplier);

/// <summary>What character creation may choose, with the numbers of the content new careers start on.</summary>
public sealed record CareerOptionsView(
    IReadOnlyList<BackgroundOptionView> Backgrounds,
    IReadOnlyList<DifficultyOptionView> Difficulties,
    IReadOnlyList<PronounSet> Pronouns);

public sealed record GetCareerOptions;

public sealed class GetCareerOptionsHandler(ICurrentContent current)
{
    public CareerOptionsView Handle(GetCareerOptions query)
    {
        var content = current.Snapshot;
        var backgrounds = Enum.GetValues<Background>()
            .Select(background => Option(background, content.Profiles[background]))
            .ToList();
        var difficulties = Enum.GetValues<Difficulty>()
            .Select(difficulty => new DifficultyOptionView(difficulty, Decimals.Of(content.Balance.DifficultyMultiplier[difficulty])))
            .ToList();
        return new CareerOptionsView(backgrounds, difficulties, Enum.GetValues<PronounSet>());
    }

    private static BackgroundOptionView Option(Background background, StartingProfile p) => new(
        background,
        p.CashPln,
        p.Followers,
        Decimals.Of(p.Drama),
        Decimals.Of(p.Credibility),
        Decimals.Of(p.Energy),
        CareerViewBuilder.Categories(p.Skill),
        CareerViewBuilder.Equipment(p.Equipment),
        Decimals.Of(p.ScoreMultiplier));
}
