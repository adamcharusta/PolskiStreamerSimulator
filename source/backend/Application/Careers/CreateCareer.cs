using System.Security.Cryptography;
using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>A new career; <see cref="CreationId"/> makes the request safe to repeat (spec M4-A §4).</summary>
public sealed record CreateCareer(string Nickname, Background Background, Difficulty Difficulty, PronounSet Pronouns, Guid? CreationId = null)
{
    // The creation ID can rotate the new career's credentials for a day; it must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Nickname = {Nickname}, Background = {Background}, Difficulty = {Difficulty}, Pronouns = {Pronouns}, CreationId = ***");
        return true;
    }
}

/// <summary>The new career; <see cref="Secret"/> and <see cref="RecoveryKey"/> are returned only here and never stored.</summary>
public sealed record CreatedCareer(Guid CareerId, string Secret, string RecoveryKey, CareerView Career)
{
    // The response body carries the secret and the key; ToString(), which loggers use, never does.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, RecoveryKey = ***, Career = {Career}");
        return true;
    }
}

public sealed class CreateCareerHandler(
    ICareerStore store,
    ICurrentContent content,
    IContentStore contents,
    ISecretHasher secrets,
    CareerViewBuilder views,
    TimeProvider time)
{
    /// <summary>How long a repeat of a creation is answered; after that, or after the first action, it is closed.</summary>
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromHours(24);

    public async Task<AppResult<CreatedCareer>> Handle(CreateCareer command, CancellationToken cancellationToken)
    {
        if (!Nicknames.IsValid(command.Nickname))
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ValidationFailed, Nicknames.Rule);
        }

        if (!Enum.IsDefined(command.Background) || !Enum.IsDefined(command.Difficulty) || !Enum.IsDefined(command.Pronouns))
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ValidationFailed, "background, difficulty or pronouns is not defined");
        }

        var nickname = Nicknames.Normalize(command.Nickname);
        var creation = command.CreationId is { } creationId ? Hashes(creationId, nickname, command) : null;
        if (creation is not null && await store.FindByCreationAsync(creation.CreationHash, cancellationToken) is { } existing)
        {
            return await RepeatAsync(existing, creation, cancellationToken);
        }

        var snapshot = content.Snapshot;
        var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        var state = CareerFactory.Create(snapshot, command.Background, command.Difficulty, command.Pronouns, seed);
        var now = time.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var secret = secrets.NewSecret();
        var recoveryKey = RecoveryKeys.Format(id, secrets.NewSecret());
        var outcome = await store.CreateAsync(
            new NewCareer(id, nickname, state, secrets.Hash(secret), secrets.Hash(recoveryKey), creation, now),
            cancellationToken);
        if (outcome == CreateOutcome.DuplicateCreation)
        {
            // A concurrent repeat stored the career first; answer as its repeat.
            var winner = await store.FindByCreationAsync(creation!.CreationHash, cancellationToken)
                ?? throw new InvalidOperationException("The career of a duplicate creation is missing.");
            return await RepeatAsync(winner, creation, cancellationToken);
        }

        var view = views.Build(id, nickname, state, snapshot, null, CareerRetention.ExpiresAt(state, now));
        return AppResult<CreatedCareer>.Ok(new CreatedCareer(id, secret, recoveryKey, view));
    }

    /// <summary>
    /// The same career with new credentials: the server never stored the first ones, and the client never received
    /// them. Only within <see cref="RepeatWindow"/> and while no action is logged.
    /// </summary>
    private async Task<AppResult<CreatedCareer>> RepeatAsync(Guid careerId, CreationHashes creation, CancellationToken cancellationToken)
    {
        var credentials = await store.FindCredentialsAsync(careerId, cancellationToken);
        var career = await store.FindAsync(careerId, cancellationToken);
        if (credentials is null || career is null)
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.CreationClosed);
        }

        if (credentials.CreationRequestHash is not { } requestHash || !CryptographicOperations.FixedTimeEquals(requestHash, creation.RequestHash))
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.CreationIdReused);
        }

        var now = time.GetUtcNow();
        var secret = secrets.NewSecret();
        var recoveryKey = RecoveryKeys.Format(careerId, secrets.NewSecret());
        var replaced = now - career.CreatedAt <= RepeatWindow
            && career.State.StateVersion == 0
            && await store.ReplaceCredentialsAsync(careerId, secrets.Hash(secret), secrets.Hash(recoveryKey), whileVersion: 0, now, cancellationToken);
        if (!replaced)
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.CreationClosed);
        }

        var snapshot = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        return AppResult<CreatedCareer>.Ok(new CreatedCareer(careerId, secret, recoveryKey, views.Build(career, snapshot, null)));
    }

    private CreationHashes Hashes(Guid creationId, string nickname, CreateCareer command)
        => new(
            secrets.Hash(creationId.ToString("D")),
            secrets.Hash($"{nickname}\n{command.Background}\n{command.Difficulty}\n{command.Pronouns}"));
}
