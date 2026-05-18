using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Common.Api;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, string successMessage = "Success")
    {
        if (result.IsSuccess) return new OkObjectResult(ApiResponse.Ok(successMessage));
        return MapFailure(result.Errors, result.Error);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, string successMessage = "Success")
    {
        if (result.IsSuccess) return new OkObjectResult(ApiResponse<T>.Ok(result.Value, successMessage));
        return MapFailure(result.Errors, result.Error);
    }

    public static IActionResult ToPagedActionResult<T>(this Result<PagedResult<T>> result, string successMessage = "Success")
    {
        if (result.IsFailure) return MapFailure(result.Errors, result.Error);

        var paged = result.Value;
        var pagination = new PaginationMetadata
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
        return new OkObjectResult(ApiResponse<IReadOnlyList<T>>.Ok(paged.Items, successMessage, pagination));
    }

    private static IActionResult MapFailure(IReadOnlyList<Error> errors, Error primary)
    {
        var details = errors.Select(e => new ErrorDetail
        {
            Code = e.Code,
            Message = e.Message,
            Field = ExtractField(e.Code)
        }).ToList();

        var response = ApiResponse.Fail(primary.Message, details);
        var status = primary.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(response) { StatusCode = status };
    }

    private static string? ExtractField(string code)
    {
        var idx = code.IndexOf('.');
        return idx > 0 ? code[..idx] : null;
    }
}
