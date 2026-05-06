namespace graphnotelm.Utils;

public class Result<T>
{
    public bool Success { get; }
    public string? Error { get; }
    public T? Value { get; }

    private Result(bool success, T? value, string? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, value, null);

    public static Result<T> Fail(string error) => new Result<T>(false, default, error);
}

public class Result
{
    public bool Success { get; }
    public string? Error { get; }

    private Result(bool success, string? error)
    {
        Success = success;
        Error = error;
    }

    public static Result Ok() => new Result(true, null);

    public static Result Fail(string error) => new Result(false, error);
}
