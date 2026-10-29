using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record RecoverCareer(string? RecoveryKey)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RecoveryKey = ***");
        return true;
    }
}

/// <summary>The recovered career and its new browser secret; the previous secret no longer opens it.</summary>
public sealed record RecoveredCareer(Guid CareerId, string Secret, CareerView Career)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Career = {Career}");
        return true;
    }
}

/// <summary>
/// Opens a career with its recovery key on any device and rotates its browser secret (spec M4-A §3). A malformed key,
/// an unknown or deleted career and a wrong key all fail alike with <see cref="ErrorCodes.RecoveryFailed"/>.
/// </summary>
public sealed class RecoverCareerHandler(ICareerStore store, IContentStore contents, ISecretHasher secrets, CareerViewBuilder views, TimeProvider time)
{
    public async Task<AppResult<RecoveredCareer>> Handle(RecoverCareer command, CancellationToken cancellationToken)
    {
        if (!RecoveryKeys.TryParse(command.RecoveryKey, out var careerId, out var key)
            || await store.FindCredentialsAsync(careerId, cancellationToken) is not { RecoveryHash: { } hash }
            || !secrets.Matches(key, hash))
        {
            return AppResult<RecoveredCareer>.Fail(ErrorCodes.RecoveryFailed);
        }

        var secret = secrets.NewSecret();
        if (!await store.ReplaceCredentialsAsync(careerId, secrets.Hash(secret), null, null, time.GetUtcNow(), cancellationToken)
            || await store.FindAsync(careerId, cancellationToken) is not { } career)
        {
            return AppResult<RecoveredCareer>.Fail(ErrorCodes.RecoveryFailed);
        }

        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        var lastReport = await store.LastReportAsync(careerId, cancellationToken);
        return AppResult<RecoveredCareer>.Ok(new RecoveredCareer(careerId, secret, views.Build(career, content, lastReport)));
    }
}

public sealed record IssueRecoveryKey(Guid CareerId, string? Secret)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

/// <summary>A new recovery key, shown once; the previous key no longer opens the career.</summary>
public sealed record IssuedRecoveryKey(Guid CareerId, string RecoveryKey)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, RecoveryKey = ***");
        return true;
    }
}

/// <summary>Issues a key for a career created before M4-A, or rotates a key the player thinks leaked.</summary>
public sealed class IssueRecoveryKeyHandler(CareerAccess access, ICareerStore store, ISecretHasher secrets, TimeProvider time)
{
    public async Task<AppResult<IssuedRecoveryKey>> Handle(IssueRecoveryKey command, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<IssuedRecoveryKey>.Fail(opened.Error);
        }

        var key = RecoveryKeys.Format(command.CareerId, secrets.NewSecret());
        return await store.ReplaceCredentialsAsync(command.CareerId, null, secrets.Hash(key), null, time.GetUtcNow(), cancellationToken)
            ? AppResult<IssuedRecoveryKey>.Ok(new IssuedRecoveryKey(command.CareerId, key))
            : AppResult<IssuedRecoveryKey>.Fail(ErrorCodes.CareerNotFound);
    }
}
