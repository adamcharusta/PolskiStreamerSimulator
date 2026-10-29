using FluentValidation;
using FluentValidation.Results;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.WebApp.Careers;

namespace PolskiStreamerSimulator.WebApp.Admin;

/// <summary>The owner's user name and password, the first sign-in step.</summary>
public sealed record OwnerLoginRequest(string Username, string Password);

public sealed class OwnerLoginRequestValidator : AbstractValidator<OwnerLoginRequest>
{
    public OwnerLoginRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(256);
    }

    protected override bool PreValidate(ValidationContext<OwnerLoginRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>Either the authenticator app's six-digit code or one unused recovery code.</summary>
public sealed record OwnerCodeRequest(string? Code = null, string? RecoveryCode = null);

public sealed class OwnerCodeRequestValidator : AbstractValidator<OwnerCodeRequest>
{
    public OwnerCodeRequestValidator()
    {
        RuleFor(r => r).Must(r => (r.Code is null) != (r.RecoveryCode is null)).WithMessage("Send either code or recoveryCode.");
        RuleFor(r => r.Code).MaximumLength(16);
        RuleFor(r => r.RecoveryCode).MaximumLength(64);
    }

    protected override bool PreValidate(ValidationContext<OwnerCodeRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>Why the owner hides or restores an entry; kept in the audit record.</summary>
public sealed record ModerationRequest(string Reason);

public sealed class ModerationRequestValidator : AbstractValidator<ModerationRequest>
{
    public ModerationRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(AuditActions.MaxReasonLength);

    protected override bool PreValidate(ValidationContext<ModerationRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>The later deadline of a closed season and why it moves.</summary>
public sealed record SeasonExtensionRequest(DateTimeOffset Until, string Reason);

public sealed class SeasonExtensionRequestValidator : AbstractValidator<SeasonExtensionRequest>
{
    public SeasonExtensionRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(AuditActions.MaxReasonLength);

    protected override bool PreValidate(ValidationContext<SeasonExtensionRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}
