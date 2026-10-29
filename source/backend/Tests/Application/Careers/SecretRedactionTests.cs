using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>Wolverine logs a failed message with its ToString(); a bearer secret must never reach a log.</summary>
public sealed class SecretRedactionTests
{
    private const string Secret = "s3cr3t-bearer-value";

    [Fact]
    public void Messages_and_results_never_print_the_secret()
    {
        var input = new ActionInput(0, ActionType.SettleWeek, null, null, null, null, null);

        Assert.DoesNotContain(Secret, new GetCareer(Guid.NewGuid(), Secret).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new SubmitAction(Guid.NewGuid(), Secret, Guid.NewGuid(), input).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new CreatedCareer(Guid.NewGuid(), Secret, Secret, null!).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new RecoverCareer(Secret).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new RecoveredCareer(Guid.NewGuid(), Secret, null!).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new IssueRecoveryKey(Guid.NewGuid(), Secret).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new IssuedRecoveryKey(Guid.NewGuid(), Secret).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, new RunBatch(Guid.NewGuid(), Secret, Guid.NewGuid(), new BatchInput(0, 13)).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_creation_never_prints_its_creation_id()
    {
        var creationId = Guid.NewGuid();

        var printed = new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, creationId).ToString();

        Assert.DoesNotContain(creationId.ToString(), printed, StringComparison.Ordinal);
        Assert.Contains("Streamer", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacted_messages_still_name_their_career()
    {
        var id = Guid.NewGuid();

        Assert.Contains(id.ToString(), new GetCareer(id, Secret).ToString(), StringComparison.Ordinal);
    }
}
