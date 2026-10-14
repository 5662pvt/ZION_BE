namespace ZIONShop.SharedKernel.Results;

public class Result
{
    protected Result(bool isSuccess, Error error, IReadOnlyList<Error>? errors = null)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException("Success result cannot carry an error.");
        if (!isSuccess && error == Error.None && (errors is null || errors.Count == 0))
            throw new InvalidOperationException("Failure result must carry at least one error.");

        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? (error == Error.None ? Array.Empty<Error>() : new[] { error });
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(IReadOnlyList<Error> errors) => new(false, errors[0], errors);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
    public static Result<T> Failure<T>(IReadOnlyList<Error> errors) => Result<T>.Failure(errors);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, Error.None) { _value = value; }
    private Result(Error error) : base(false, error) { _value = default; }
    private Result(IReadOnlyList<Error> errors) : base(false, errors[0], errors) { _value = default; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    public new static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);
    public new static Result<T> Failure(IReadOnlyList<Error> errors) => new(errors);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
