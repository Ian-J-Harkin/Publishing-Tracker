namespace PublishingTracker.Domain;

/// <summary>
/// A generic Result wrapper for explicit error handling and avoiding null returns.
/// </summary>
public class Result<T>
{
    public T? Value { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    protected Result(T? value, bool isSuccess, string? errorMessage)
    {
        Value = value;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new Result<T>(value, true, null);
    public static Result<T> Failure(string errorMessage) => new Result<T>(default, false, errorMessage);
}

/// <summary>
/// A non-generic Result wrapper.
/// </summary>
public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    protected Result(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new Result(true, null);
    public static Result Failure(string errorMessage) => new Result(false, errorMessage);
}
