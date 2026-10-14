using FluentValidation;

namespace ZIONShop.Inventory.Application.Features.GetAvailabilityBatch;

public class GetAvailabilityBatchQueryValidator : AbstractValidator<GetAvailabilityBatchQuery>
{
    public GetAvailabilityBatchQueryValidator()
    {
        RuleFor(x => x.ProductIds)
            .Must(ids => ids.Count <= 100)
            .WithMessage("Tối đa 100 productId mỗi lần truy vấn");
    }
}
