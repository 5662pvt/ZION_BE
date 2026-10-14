using System.Text.Json;

namespace ZIONShop.Api.IntegrationTests.Infrastructure;

public record ApiEnvelope<T>(bool Success, string Message, T? Data, List<ErrorDetailDto>? Errors, PaginationDto? Pagination);

public record ErrorDetailDto(string? Field, string? Code, string? Message);

public record PaginationDto(int Page, int PageSize, int TotalCount, int TotalPages);

public static class ApiEnvelopeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
