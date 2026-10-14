using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportPromotionsCsv;

public record ExportPromotionsCsvQuery(string? From, string? To) : IRequest<Result<CsvFile>>;
