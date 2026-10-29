using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Content;

/// <summary>Spec M4-E §3, §5–§6: the draft workflow, publication, seasons, reversal and export.</summary>
public sealed class ContentAdministrationTests
{
    private const string Owner = "wlasciciel";

    private readonly AppTestKit kit = AppTestKit.WithContent(TestPaths.ContentM3);
    private readonly PublishedContent published = new();
    private readonly InMemoryContentAdminStore store;
    private readonly ContentWorkspace workspace;

    public ContentAdministrationTests()
    {
        store = new InMemoryContentAdminStore(kit.AuditStore);
        workspace = new ContentWorkspace(store, kit.ContentStore, new JsonContentCompiler(), kit.Engine, new ContentSimulation(kit.Engine), published, kit.Seasons, kit.Time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>What startup does: store the repository content, publish it as a release and pin it.</summary>
    private async Task StartAsync()
    {
        await kit.StartAsync();
        var files = kit.Current.Files;
        var release = await store.EnsureReleasedAsync(
            new NewPublication(kit.Current.Snapshot.Digest, kit.Current.MechanicsHash, PublicationKind.Release, null, ContentAuditActions.DeploymentActor, "repository content", kit.Time.GetUtcNow()),
            new AuditEntry(ContentAuditActions.DeploymentActor, ContentAuditActions.Release, kit.Current.Snapshot.Digest, "repository content", null, null, kit.Time.GetUtcNow()),
            Ct);
        published.Set(release.Number, files, kit.Current.Snapshot);
    }

    private async Task<ContentDraftView> DraftAsync()
        => (await new CreateContentDraftHandler(workspace, store, store).Handle(new CreateContentDraft(null, Owner), Ct)).Value;

    private Task<AppResult<ContentDraftView>> PutEventAsync(int revision, JsonObject item)
        => new PutDraftEventHandler(workspace, store).Handle(new PutDraftEvent(item["id"]!.GetValue<string>(), revision, JsonSerializer.SerializeToElement(item), Owner), Ct);

    private Task<AppResult<ContentPublicationView>> PublishAsync(int revision, string? reason = "Poprawka")
        => new PublishContentDraftHandler(workspace, store).Handle(new PublishContentDraft(revision, reason, Owner), Ct);

    private CreateCareerHandler Careers() => new(kit.Careers, published, kit.ContentStore, kit.Seasons, kit.Secrets, kit.Views, kit.Time);

    private Task<AppResult<CreatedCareer>> RankedAsync()
        => Careers().Handle(new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, Ranked: true), Ct);

    private static JsonObject FirstEvent(ContentDraftView draft) => JsonNode.Parse(draft.Events[0].Event.GetRawText())!.AsObject();

    [Fact]
    public async Task A_text_fix_publishes_a_new_digest_in_the_same_season()
    {
        await StartAsync();
        var before = (await RankedAsync()).Value;
        var draft = await DraftAsync();
        var item = FirstEvent(draft);
        item["title"] = "Nowy tytuł";
        item["version"] = item["version"]!.GetValue<int>() + 1;

        var edited = (await PutEventAsync(draft.Revision, item)).Value;
        var publication = (await PublishAsync(edited.Revision)).Value;
        var after = (await RankedAsync()).Value;

        Assert.Empty(edited.Problems);
        Assert.False(edited.Diff.Mechanical);
        Assert.Equal((2, PublicationKind.Publish, Owner), (publication.Number, publication.Kind, publication.Actor));
        Assert.Equal(kit.Current.MechanicsHash, publication.MechanicsHash);
        Assert.Equal((1, 1), (before.Career.Season, after.Career.Season));
        Assert.Equal(publication.Digest, (await kit.Careers.FindAsync(after.CareerId, Ct))!.State.ContentDigest);
        Assert.Equal(kit.Current.Snapshot.Digest, (await kit.Careers.FindAsync(before.CareerId, Ct))!.State.ContentDigest);
        Assert.Null(await ((IContentDrafts)store).FindAsync(Ct));
    }

    [Fact]
    public async Task A_mechanical_change_opens_a_new_season_and_running_careers_keep_their_content()
    {
        await StartAsync();
        var before = (await RankedAsync()).Value;
        var draft = await DraftAsync();
        var item = FirstEvent(draft);
        item["weight"] = item["weight"]!.GetValue<int>() + 1;
        item["version"] = item["version"]!.GetValue<int>() + 1;
        var edited = (await PutEventAsync(draft.Revision, item)).Value;

        Assert.True(edited.Diff.Mechanical);
        Assert.True((await PublishAsync(edited.Revision)).IsSuccess);

        var after = (await RankedAsync()).Value;
        Assert.Equal(2, after.Career.Season);
        Assert.NotNull((await kit.Seasons.FindAsync(1, Ct))!.ClosedAt);
        kit.UseSecret(before.CareerId, before.Secret);
        var played = await kit.GetAsync(before.CareerId);
        Assert.Equal(1, played.Season);
        Assert.Equal(kit.Current.Snapshot.Digest, (await kit.Careers.FindAsync(before.CareerId, Ct))!.State.ContentDigest);
    }

    [Fact]
    public async Task An_edited_event_that_keeps_its_published_version_cannot_be_published()
    {
        await StartAsync();
        var draft = await DraftAsync();
        var item = FirstEvent(draft);
        item["title"] = "Ten sam numer wersji";

        var edited = (await PutEventAsync(draft.Revision, item)).Value;
        var refused = await PublishAsync(edited.Revision);

        Assert.Contains(edited.Problems, p => p.StartsWith("event-version-changed:", StringComparison.Ordinal));
        Assert.Equal(ErrorCodes.ContentInvalid, refused.Error.Code);
    }

    [Fact]
    public async Task An_invalid_draft_is_saved_with_its_problems_but_not_published()
    {
        await StartAsync();
        var draft = await DraftAsync();
        var item = FirstEvent(draft);
        item["weight"] = -1;
        item["version"] = item["version"]!.GetValue<int>() + 1;

        var edited = (await PutEventAsync(draft.Revision, item)).Value;

        Assert.NotEmpty(edited.Problems);
        Assert.Equal(ErrorCodes.ContentInvalid, (await PublishAsync(edited.Revision)).Error.Code);
        Assert.Equal(1, published.Number);
    }

    [Fact]
    public async Task An_unchanged_draft_and_a_missing_reason_are_refused()
    {
        await StartAsync();
        var draft = await DraftAsync();

        Assert.True(draft.Diff.Unchanged);
        Assert.Equal(ErrorCodes.ContentUnchanged, (await PublishAsync(draft.Revision)).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await PublishAsync(draft.Revision, "  ")).Error.Code);
    }

    [Fact]
    public async Task Edits_need_the_current_revision_and_are_audited()
    {
        await StartAsync();
        var draft = await DraftAsync();
        var item = FirstEvent(draft);
        item["title"] = "A";

        var first = await PutEventAsync(draft.Revision, item);
        var stale = await PutEventAsync(draft.Revision, item);
        var removed = await new RemoveDraftEventHandler(workspace, store).Handle(new RemoveDraftEvent(item["id"]!.GetValue<string>(), first.Value.Revision, Owner), Ct);
        var missing = await new RemoveDraftEventHandler(workspace, store).Handle(new RemoveDraftEvent("nie-ma", removed.Value.Revision, Owner), Ct);
        var duplicate = await new CreateContentDraftHandler(workspace, store, store).Handle(new CreateContentDraft(null, Owner), Ct);

        Assert.Equal(2, first.Value.Revision);
        Assert.Equal(ErrorCodes.DraftStale, stale.Error.Code);
        Assert.Equal(draft.Events.Count - 1, removed.Value.Events.Count);
        Assert.Contains(removed.Value.Diff.Items, i => i.Change == ContentChange.Removed);
        Assert.Equal(ErrorCodes.EventNotFound, missing.Error.Code);
        Assert.Equal(ErrorCodes.DraftExists, duplicate.Error.Code);
        Assert.Equal(
            [ContentAuditActions.Release, ContentAuditActions.DraftCreate, ContentAuditActions.DraftEvent, ContentAuditActions.DraftEventRemove],
            kit.AuditStore.All.Select(r => r.Entry.Action));
    }

    [Fact]
    public async Task An_event_body_must_carry_the_id_of_its_path()
    {
        await StartAsync();
        var draft = await DraftAsync();

        var result = await new PutDraftEventHandler(workspace, store)
            .Handle(new PutDraftEvent("inne-id", draft.Revision, JsonSerializer.SerializeToElement(FirstEvent(draft)), Owner), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task A_revert_publishes_the_earlier_digest_again_in_a_new_season()
    {
        await StartAsync();
        var draft = await DraftAsync();
        var balance = JsonNode.Parse(draft.Balance.GetRawText())!.AsObject();
        balance["livingCostPln"] = 120;
        var edited = (await new PutDraftBalanceHandler(workspace, store).Handle(new PutDraftBalance(draft.Revision, JsonSerializer.SerializeToElement(balance), Owner), Ct)).Value;
        Assert.Equal("livingCostPln", Assert.Single(edited.Diff.Values).Path);
        Assert.True((await PublishAsync(edited.Revision)).IsSuccess);

        var reverted = (await new RevertContentPublicationHandler(workspace, store).Handle(new RevertContentPublication(1, "Za drogo", Owner), Ct)).Value;
        var again = await new RevertContentPublicationHandler(workspace, store).Handle(new RevertContentPublication(3, "Jeszcze raz", Owner), Ct);

        Assert.Equal((3, PublicationKind.Revert, 1), (reverted.Number, reverted.Kind, reverted.SourceNumber));
        Assert.Equal(kit.Current.Snapshot.Digest, reverted.Digest);
        Assert.Equal(3, (await RankedAsync()).Value.Career.Season);
        Assert.Equal(ErrorCodes.ContentUnchanged, again.Error.Code);
        Assert.Equal([3, 2, 1], (await new ListContentPublicationsHandler(store).Handle(ListContentPublications.Instance, Ct)).Select(p => p.Number));
        Assert.Equal(ContentAuditActions.Revert, kit.AuditStore.All[^1].Entry.Action);
    }

    [Fact]
    public async Task An_export_returns_the_stored_bytes_of_its_digest()
    {
        await StartAsync();

        var export = (await new ExportContentPublicationHandler(workspace, store).Handle(new ExportContentPublication(1), Ct)).Value;
        var missing = await new ExportContentPublicationHandler(workspace, store).Handle(new ExportContentPublication(9), Ct);

        var digest = JsonContentSource.Parse(Encoding.UTF8.GetBytes(export.BalanceJson), Encoding.UTF8.GetBytes(export.EventsJson), "export").Digest;
        Assert.Equal(export.Digest, digest);
        Assert.Equal(ErrorCodes.PublicationNotFound, missing.Error.Code);
    }

    [Fact]
    public async Task The_simulation_replays_the_draft_and_the_current_content_alike()
    {
        await StartAsync();
        await DraftAsync();

        var simulation = (await new SimulateContentDraftHandler(workspace, store, new ContentSimulation(kit.Engine), published).Handle(SimulateContentDraft.Instance, Ct)).Value;

        Assert.True(simulation.Passed);
        Assert.Equal(ContentSimulation.Careers, simulation.Draft.Endings.Values.Sum());
        Assert.Equal(simulation.Current.Endings, simulation.Draft.Endings);
        Assert.Equal((simulation.Current.Score, simulation.Current.Weeks), (simulation.Draft.Score, simulation.Draft.Weeks));
        Assert.Empty(simulation.Draft.Violations);
    }
}
