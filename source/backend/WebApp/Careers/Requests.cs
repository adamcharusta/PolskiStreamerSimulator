using FluentValidation;
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
}

public sealed record ActionRequest(
    Guid CommandId,
    long ExpectedVersion,
    ActionType Type,
    MainAction? MainAction,
    Category? Category,
    Category? TrainTarget,
    EquipmentSlot? UpgradeSlot,
    string? ChoiceId);

public sealed class ActionRequestValidator : AbstractValidator<ActionRequest>
{
    public ActionRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Type).IsInEnum();
        When(r => r.Type == ActionType.StartWeek, () =>
        {
            RuleFor(r => r.MainAction).NotNull().IsInEnum();
            RuleFor(r => r.Category).NotNull().IsInEnum();
        });
        When(r => r.Type == ActionType.ResolveEvent, () => RuleFor(r => r.ChoiceId).NotEmpty().MaximumLength(64));
    }
}
