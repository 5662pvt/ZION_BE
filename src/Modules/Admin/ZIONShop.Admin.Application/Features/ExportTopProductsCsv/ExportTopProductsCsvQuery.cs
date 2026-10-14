using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportTopProductsCsv;

public record ExportTopProductsCsvQuery(string? From, string? To) : IRequest<Result<CsvFile>>;
