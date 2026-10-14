using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportRevenueCsv;

public record ExportRevenueCsvQuery(string? From, string? To) : IRequest<Result<CsvFile>>;
