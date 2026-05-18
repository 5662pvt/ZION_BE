namespace ZIONShop.Common.Api;

public class ErrorDetail
{
    public string? Field { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
