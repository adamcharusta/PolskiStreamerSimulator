using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// Applies an endpoint's <see cref="RequestSizeLimitAttribute"/> before the body is read: a declared length over the
/// limit is <c>413 payload-too-large</c>, and the server stops reading a body without one at the limit (spec M4-C §5).
/// </summary>
public static class RequestSizeLimits
{
    public static IApplicationBuilder UseRequestSizeLimits(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>() is { MaxRequestBodySize: { } limit })
            {
                if (context.Request.ContentLength > limit)
                {
                    await RejectAsync(context);
                    return;
                }

                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
                {
                    feature.MaxRequestBodySize = limit;
                }
            }

            await next(context);
        });

    private static async Task RejectAsync(HttpContext context)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status413PayloadTooLarge, Title = ErrorCodes.PayloadTooLarge };
        problem.Extensions["code"] = ErrorCodes.PayloadTooLarge;
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", context.RequestAborted);
    }
}
