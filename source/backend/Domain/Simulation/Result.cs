namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>Either a value or a <see cref="DomainError"/>; commands never throw for rule violations.</summary>
public readonly record struct Result<T>
{
    private readonly T? value;
    private readonly DomainError? error;

    private Result(T? value, DomainError? error)
    {
        this.value = value;
        this.error = error;
    }

    public bool IsSuccess => error is null;

    public T Value => IsSuccess ? value! : throw new InvalidOperationException($"Result failed with {error!.Code}: {error.Detail}");

    public DomainError Error => error ?? throw new InvalidOperationException("Result succeeded; there is no error.");

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(DomainError error) => new(default, error);

    public static Result<T> Fail(DomainErrorCode code, string detail) => new(default, new DomainError(code, detail));
}
