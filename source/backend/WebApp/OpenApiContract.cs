using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// Shapes the OpenAPI document for the generated client: the bearer career secret, string enums, Problem Details with
/// their code, a documented 500 and readable operation names (TD-101).
/// </summary>
public static class OpenApiContract
{
    public const string SecurityScheme = "careerSecret";

    private const string Tag = "careers";

    private static readonly Dictionary<string, (string Summary, bool Secured)> Operations = new(StringComparer.Ordinal)
    {
        ["createCareer"] = ("Create a career and receive its secret and recovery key once", false),
        ["recoverCareer"] = ("Open a career with its recovery key and receive a new secret", false),
        ["issueRecoveryKey"] = ("Issue a new recovery key; the previous one stops working", true),
        ["getCareerOptions"] = ("List the starting profiles and difficulties", false),
        ["getCareer"] = ("Read a career and its next step", true),
        ["submitAction"] = ("Settle one manual step", true),
        ["runBatch"] = ("Play 4, 13 or 52 weeks automatically", true),
        ["getHistory"] = ("List the settled weeks of a career", true),
        ["getHistoryWeek"] = ("Read the stored report of one week", true),
        ["getLeaderboardEntry"] = ("Read whether and until when a career can publish its result", true),
        ["publishResult"] = ("Publish the server-computed result of an ended ranked career under a nickname", true),
        ["getLeaderboard"] = ("Read the top entries of a season", false),
        ["listSeasons"] = ("List the seasons and their submission deadlines", false),
        ["listSaveSlots"] = ("List the three manual slots of an unranked career", true),
        ["saveToSlot"] = ("Save the current state of an unranked career into a slot", true),
        ["restoreSlot"] = ("Restore an unranked career to the state saved in a slot", true),
        ["exportCareer"] = ("Export an unranked career as a save file", true),
        ["importCareer"] = ("Create a new unranked career from a save file", false),
    };

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo { Title = "Polski Streamer Simulator API", Version = "v1" };
            document.Tags = new HashSet<OpenApiTag> { new() { Name = Tag, Description = "Anonymous careers: creation, recovery, reading, steps and batches." } };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SecurityScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "The career secret returned once by createCareer, sent as Authorization: Bearer <secret>.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var id = operation.OperationId ?? string.Empty;
            var (summary, secured) = Operations.TryGetValue(id, out var known) ? known : (id, false);
            operation.Summary = summary;
            operation.Description = null;
            operation.Tags = new HashSet<OpenApiTagReference> { new(Tag, context.Document) };
            if (secured)
            {
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SecurityScheme, context.Document)] = [] }];
            }

            operation.Responses ??= [];
            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Internal Server Error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(nameof(ProblemDetails), context.Document) },
                },
            };
            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (type.IsEnum)
            {
                // Nullability lives on the referencing property (oneOf with null), never in the shared enum.
                schema.Type = JsonSchemaType.String;
                schema.Enum = schema.Enum?.Where(value => value is not null).ToList();
            }

            if (type == typeof(ProblemDetails))
            {
                schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable error code, for example stale-version." };
                schema.Properties["errors"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Field errors of validation-failed, keyed by camelCase field name.",
                    AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                };
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                schema.Required.Add("code");
            }

            return Task.CompletedTask;
        });
    }
}
