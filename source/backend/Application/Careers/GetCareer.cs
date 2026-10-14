using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record GetCareer(Guid CareerId, string? Secret)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

public sealed class GetCareerHandler(CareerAccess access, ICareerStore store, IContentStore contents, CareerViewBuilder views)
{
    public async Task<AppResult<CareerView>> Handle(GetCareer query, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(query.CareerId, query.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<CareerView>.Fail(opened.Error);
        }

        var career = opened.Value;
        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        var lastReport = await store.LastReportAsync(career.Id, cancellationToken);
        return AppResult<CareerView>.Ok(views.Build(career.Id, career.Nickname, career.State, content, lastReport));
    }
}
