using FluentValidation;

namespace ZIONShop.Admin.Application.Features.ListAuditLogs;

public class ListAuditLogsQueryValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsQueryValidator()
    {
        RuleFor(x => x.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value.Date <= query.To.Value.Date)
            .WithMessage("'from' phải nhỏ hơn hoặc bằng 'to'");
    }
}
