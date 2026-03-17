namespace FindThatBook.Application.Common;

/// <summary>
/// A discriminated union representing success or failure without throwing exceptions for business logic.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public int? ErrorCode { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error, int errorCode) { IsSuccess = false; Error = error; ErrorCode = errorCode; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error, int errorCode = 500) => new(error, errorCode);

    public static implicit operator Result<T>(T value) => Success(value);
}
