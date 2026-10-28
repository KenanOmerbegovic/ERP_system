namespace Erp.SharedKernel.Results;

/// <summary>
/// Outcome of an operation that can fail in an expected way. Use exceptions only for bugs.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (isSuccess != (error == Error.None))
        {
            throw new ArgumentException("A success has no error and a failure must have one.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The value of a successful result. Reading it from a failure is a bug and throws.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error.Code}).");

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
