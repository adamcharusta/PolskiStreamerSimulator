using FluentValidation;
using FluentValidation.Results;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.WebApp.Careers;

public sealed record CreateCareerRequest(string Nickname, Background Background, Difficulty Difficulty, PronounSet Pronouns);

public sealed class CreateCareerRequestValidator : AbstractValidator<CreateCareerRequest>
{
    public CreateCareerRequestValidator()
    {
        RuleFor(r => r.Nickname).Must(Nicknames.IsValid).WithMessage(Nicknames.Rule);
        RuleFor(r => r.Background).IsInEnum();
        RuleFor(r => r.Difficulty).IsInEnum();
        RuleFor(r => r.Pronouns).IsInEnum();
    }

    protected override bool PreValidate(ValidationContext<CreateCareerRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>Only the fields of the chosen <see cref="Type"/> are needed; the others may be omitted.</summary>
public sealed record ActionRequest(
    Guid CommandId,
    long ExpectedVersion,
    ActionType Type,
    MainAction? MainAction = null,
    Category? Category = null,
    Category? TrainTarget = null,
    EquipmentSlot? UpgradeSlot = null,
    string? ChoiceId = null);

public sealed class ActionRequestValidator : AbstractValidator<ActionRequest>
{
    public ActionRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.TrainTarget).IsInEnum();
        RuleFor(r => r.UpgradeSlot).IsInEnum();
        When(r => r.Type == ActionType.StartWeek, () =>
        {
            RuleFor(r => r.MainAction).NotNull().IsInEnum();
            RuleFor(r => r.Category).NotNull().IsInEnum();
        });
        When(r => r.Type == ActionType.ResolveEvent, () => RuleFor(r => r.ChoiceId).NotEmpty().MaximumLength(64));
    }

    protected override bool PreValidate(ValidationContext<ActionRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public sealed record BatchRequest(Guid CommandId, long ExpectedVersion, int Weeks);

public sealed class BatchRequestValidator : AbstractValidator<BatchRequest>
{
    public BatchRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Weeks).Equal(RunBatchHandler.Weeks);
    }

    protected override bool PreValidate(ValidationContext<BatchRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

internal static class RequestBodies
{
    /// <summary>A JSON body of <c>null</c> is a validation failure, not a server error.</summary>
    public static bool Present(object? body, ValidationResult result)
    {
        if (body is not null)
        {
            return true;
        }

        result.Errors.Add(new ValidationFailure(string.Empty, "A request body is required."));
        return false;
    }
}
