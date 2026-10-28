using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>Rolls carry the authored title, label and outcome text of the career's snapshot (#82).</summary>
public sealed class RollTextTests
{
    private static readonly ContentSnapshot C = TestContent.Of(TestPaths.ContentM3);

    [Fact]
    public void A_live_event_roll_carries_title_label_and_success_or_failure_text()
    {
        var e = C.Events.First(x => x.Id == "jc-hot-take");
        var success = RollTexts.Of(new RollResult(e.Id, "defend", 10, 50, [], RollOutcome.Success), C);
        Assert.Equal((e.Title, e.Choices[0].LabelPl, e.Choices[0].SuccessText), (success.EventTitle, success.ChoiceLabel, success.OutcomeText));
        var failure = RollTexts.Of(new RollResult(e.Id, "defend", 90, 50, [], RollOutcome.CriticalFailure), C);
        Assert.Equal(e.Choices[0].FailureText, failure.OutcomeText);
        var guaranteed = RollTexts.Of(new RollResult(e.Id, "retract", null, 100, [], RollOutcome.Guaranteed), C);
        Assert.Equal(e.Choices[1].SuccessText, guaranteed.OutcomeText);
    }

    [Fact]
    public void Network_and_pitch_rolls_use_the_balance_choices()
    {
        var network = RollTexts.Of(new RollResult("network", "collaborate", 20, 35, [], RollOutcome.Failure), C);
        Assert.Equal((null, C.Balance.NetworkCollaborate!.LabelPl, C.Balance.NetworkCollaborate.FailureText), (network.EventTitle, network.ChoiceLabel, network.OutcomeText));
        var pitch = RollTexts.Of(new RollResult("pitch", "pitch", 5, 15, [], RollOutcome.Success), C);
        Assert.Equal(C.Balance.Sponsors!.Pitch.SuccessText, pitch.OutcomeText);
    }

    [Fact]
    public void A_ban_move_roll_is_named_by_the_platform_ban_text()
    {
        var move = RollTexts.Of(new RollResult("platform-ban", "move-Nurt", null, 100, [], RollOutcome.Guaranteed), C);
        Assert.Equal((C.Balance.PlatformBan!.Title, C.Balance.PlatformBan.Choices[Platform.Nurt], (string?)null), (move.EventTitle, move.ChoiceLabel, move.OutcomeText));
    }

    [Fact]
    public void An_unknown_event_keeps_its_ids_only()
    {
        var roll = RollTexts.Of(new RollResult("gone", "x", 1, 50, [], RollOutcome.Success), C);
        Assert.Equal(((string?)null, (string?)null, (string?)null), (roll.EventTitle, roll.ChoiceLabel, roll.OutcomeText));
    }

    [Fact]
    public async Task A_settled_week_report_has_roll_texts()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = await kit.PlayManualWeekAsync(await kit.NewCareerAsync());
        Assert.All(view.LastReport!.Rolls, r => Assert.False(string.IsNullOrEmpty(r.ChoiceLabel)));
    }
}
