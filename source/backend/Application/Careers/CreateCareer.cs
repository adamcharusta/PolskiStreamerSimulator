using System.Security.Cryptography;
using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record CreateCareer(string Nickname, Background Background, Difficulty Difficulty, PronounSet Pronouns);

/// <summary>The new career; <see cref="Secret"/> is returned only here and never stored.</summary>
public sealed record CreatedCareer(Guid CareerId, string Secret, CareerView Career)
{
    // The response body carries the secret; ToString(), which loggers use, never does.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Career = {Career}");
        return true;
    }
}

public sealed class CreateCareerHandler(ICareerStore store, ICurrentContent content, ISecretHasher secrets, CareerViewBuilder views, TimeProvider time)
{
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

        var snapshot = content.Snapshot;
        var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        var state = CareerFactory.Create(
            snapshot.RulesetId,
            snapshot.Digest,
            snapshot.Profiles[command.Background],
            command.Background,
            command.Difficulty,
            command.Pronouns,
            seed);
        var now = time.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var secret = secrets.NewSecret();
        var nickname = Nicknames.Normalize(command.Nickname);
        await store.CreateAsync(new NewCareer(id, nickname, state, secrets.Hash(secret), now), cancellationToken);
        return AppResult<CreatedCareer>.Ok(new CreatedCareer(id, secret, views.Build(id, nickname, state, snapshot, null)));
    }
}
