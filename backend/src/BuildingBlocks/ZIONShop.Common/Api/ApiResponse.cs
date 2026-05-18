namespace ZIONShop.Common.Api;

public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public IReadOnlyList<ErrorDetail> Errors { get; set; } = Array.Empty<ErrorDetail>();
    public PaginationMetadata? Pagination { get; set; }

    public static ApiResponse Ok(string message = "Success") => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IReadOnlyList<ErrorDetail>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? Array.Empty<ErrorDetail>() };
}

public class ApiResponse<T> : ApiResponse
{
    public new T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success", PaginationMetadata? pagination = null) =>
        new() { Success = true, Message = message, Data = data, Pagination = pagination };

    public new static ApiResponse<T> Fail(string message, IReadOnlyList<ErrorDetail>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? Array.Empty<ErrorDetail>() };
}
