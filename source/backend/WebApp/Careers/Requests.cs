using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.WebApp.Careers;

/// <summary><see cref="CreationId"/> is a fresh random UUID per creation; repeating it repeats the creation (TD-121). A ranked career plays for the open season (TD-122).</summary>
public sealed record CreateCareerRequest(
    string Nickname,
    Background Background,
    Difficulty Difficulty,
    PronounSet Pronouns,
    Guid? CreationId = null,
    bool Ranked = false);

public sealed class CreateCareerRequestValidator : AbstractValidator<CreateCareerRequest>
{
    public CreateCareerRequestValidator()
    {
        RuleFor(r => r.Nickname).Must(Nicknames.IsValid).WithMessage(Nicknames.Rule);
        RuleFor(r => r.Background).IsInEnum();
        RuleFor(r => r.Difficulty).IsInEnum();
        RuleFor(r => r.Pronouns).IsInEnum();
        RuleFor(r => r.CreationId).NotEqual(Guid.Empty);
    }

    protected override bool PreValidate(ValidationContext<CreateCareerRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public sealed record RecoverCareerRequest(string RecoveryKey);

public sealed class RecoverCareerRequestValidator : AbstractValidator<RecoverCareerRequest>
{
    public RecoverCareerRequestValidator() => RuleFor(r => r.RecoveryKey).NotEmpty().MaximumLength(200);

    protected override bool PreValidate(ValidationContext<RecoverCareerRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>Only the nickname: the server publishes the score it computed, and any other field is ignored.</summary>
public sealed record PublishResultRequest(string Nickname);

public sealed class PublishResultRequestValidator : AbstractValidator<PublishResultRequest>
{
    // The handler applies the nickname rule after the eligibility checks, so a repeat returns the entry it already has.
    public PublishResultRequestValidator() => RuleFor(r => r.Nickname).NotNull().MaximumLength(50);

    protected override bool PreValidate(ValidationContext<PublishResultRequest> context, ValidationResult result)
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
    string? ChoiceId = null,
    WagerTier? Wager = null,
    Platform? TargetPlatform = null,
    string? OfferId = null,
    bool? Accept = null);

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
        RuleFor(r => r.Wager).IsInEnum();
        RuleFor(r => r.TargetPlatform).IsInEnum();
        When(r => r.Type is ActionType.ResolveEvent or ActionType.ResolveDecision, () => RuleFor(r => r.ChoiceId).NotEmpty().MaximumLength(64));
        When(r => r.Type == ActionType.RespondOffer, () =>
        {
            RuleFor(r => r.OfferId).NotEmpty().MaximumLength(64);
            RuleFor(r => r.Accept).NotNull();
        });
    }

    protected override bool PreValidate(ValidationContext<ActionRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public sealed record BatchRequest(Guid CommandId, long ExpectedVersion, int Weeks, StrategyInput? Strategy = null);

public sealed class BatchRequestValidator : AbstractValidator<BatchRequest>
{
    public BatchRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Weeks).Must(RunBatchHandler.AllowedWeeks.Contains).WithMessage("Weeks must be 4, 13 or 52.");
        When(r => r.Strategy is not null, () =>
        {
            RuleFor(r => r.Strategy!.CategoryPlan).IsInEnum();
            RuleFor(r => r.Strategy!.Focus).IsInEnum();
            RuleFor(r => r.Strategy!.Risk).IsInEnum();
            RuleFor(r => r.Strategy!.Wager).IsInEnum();
            RuleFor(r => r.Strategy!.Routine).NotNull();
            RuleForEach(r => r.Strategy!.Routine).IsInEnum();
            RuleFor(r => r.Strategy!).Must(s => s.Routine is null || s.ToStrategy().Errors().Count == 0).WithMessage(s => string.Join(", ", s.Strategy!.ToStrategy().Errors()));
        });
    }

    protected override bool PreValidate(ValidationContext<BatchRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>The state version the player is looking at; a stale one is rejected (spec M4-C §3).</summary>
public sealed record SaveToSlotRequest(long ExpectedVersion);

public sealed class SaveToSlotRequestValidator : AbstractValidator<SaveToSlotRequest>
{
    public SaveToSlotRequestValidator() => RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);

    protected override bool PreValidate(ValidationContext<SaveToSlotRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary><see cref="CommandId"/> makes the restore safe to repeat, like a command (spec M4-C §3).</summary>
public sealed record RestoreSlotRequest(Guid CommandId, long ExpectedVersion);

public sealed class RestoreSlotRequestValidator : AbstractValidator<RestoreSlotRequest>
{
    public RestoreSlotRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEqual(Guid.Empty);
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
    }

    protected override bool PreValidate(ValidationContext<RestoreSlotRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>The exported save file as it was downloaded (spec M4-C §5).</summary>
public sealed record ImportCareerRequest(JsonElement? Save);

public sealed class ImportCareerRequestValidator : AbstractValidator<ImportCareerRequest>
{
    public ImportCareerRequestValidator() => RuleFor(r => r.Save).NotNull();

    protected override bool PreValidate(ValidationContext<ImportCareerRequest> context, ValidationResult result)
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
