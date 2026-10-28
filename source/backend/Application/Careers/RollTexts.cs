using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// M3-D (#82): a roll with the authored text of the career's pinned snapshot: the event title, the choice label and
/// the outcome text (success text for a success, critical success or guaranteed choice; failure text, else success
/// text, for a failure). Networking and the sponsor pitch read their balance choices; the ruleset-built platform-ban
/// decision reads <c>balance.platformBan</c>. An event missing from the snapshot keeps only its IDs.
/// </summary>
public static class RollTexts
{
    private const string NetworkEventId = "network";
    private const string PitchEventId = "pitch";

    public static RollView Of(RollResult roll, ContentSnapshot c)
    {
        var view = ReportViews.Roll(roll);
        if (roll.EventId == PlatformRules.BanDecisionId && c.Balance.PlatformBan is { } ban)
        {
            var label = Enum.TryParse<Platform>(roll.ChoiceId.Replace("move-", string.Empty, StringComparison.Ordinal), out var platform)
                && ban.Choices.TryGetValue(platform, out var text) ? text : null;
            return view with { EventTitle = ban.Title, ChoiceLabel = label };
        }

        var (title, choice) = Find(roll, c);
        return choice is null
            ? view
            : view with { EventTitle = title, ChoiceLabel = choice.LabelPl, OutcomeText = OutcomeText(choice, roll.Outcome) };
    }

    private static (string? Title, ChoiceDefinition? Choice) Find(RollResult roll, ContentSnapshot c)
    {
        var balanceChoice = roll.EventId switch
        {
            NetworkEventId => c.Balance.NetworkCollaborate,
            PitchEventId => c.Balance.Sponsors?.Pitch,
            _ => null,
        };
        if (balanceChoice is not null)
        {
            return (null, balanceChoice);
        }

        var e = c.Events.FirstOrDefault(x => string.Equals(x.Id, roll.EventId, StringComparison.Ordinal));
        return (e?.Title, e?.Choices.FirstOrDefault(x => string.Equals(x.Id, roll.ChoiceId, StringComparison.Ordinal)));
    }

    private static string? OutcomeText(ChoiceDefinition choice, RollOutcome outcome)
        => outcome is RollOutcome.Failure or RollOutcome.CriticalFailure ? choice.FailureText ?? choice.SuccessText : choice.SuccessText;
}
