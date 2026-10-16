using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

public static class DomainErrors
{
    /// <summary>Maps a rule rejection to its stable code; an unknown ruleset or mismatched content is a server fault.</summary>
    public static AppError ToAppError(DomainError error) => error.Code switch
    {
        DomainErrorCode.IllegalAction => new AppError(ErrorCodes.IllegalAction, error.Detail),
        DomainErrorCode.StaleVersion or DomainErrorCode.WeekAlreadySettled => new AppError(ErrorCodes.StaleVersion, error.Detail),
        DomainErrorCode.NoPendingWeek => new AppError(ErrorCodes.NoPendingWeek, error.Detail),
        DomainErrorCode.EventsUnresolved => new AppError(ErrorCodes.EventsUnresolved, error.Detail),
        DomainErrorCode.CareerEnded => new AppError(ErrorCodes.CareerEnded, error.Detail),
        DomainErrorCode.UnknownChoice => new AppError(ErrorCodes.UnknownChoice, error.Detail),
        _ => throw new InvalidOperationException($"Server fault: {error.Code} ({error.Detail})."),
    };
}
