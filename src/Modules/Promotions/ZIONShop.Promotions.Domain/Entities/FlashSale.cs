using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Entities;

public class FlashSale : BaseEntity
{
    private FlashSale() { }

    public string Name { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public bool IsActive { get; private set; }

    public static FlashSale Create(string name, DateTime startsAt, DateTime endsAt) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        StartsAt = startsAt,
        EndsAt = endsAt,
        IsActive = false
    };

    public void Update(string name, DateTime startsAt, DateTime endsAt)
    {
        Name = name;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public Result Activate()
    {
        if (IsActive) return Result.Failure(PromotionsErrors.AlreadyActive);
        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Failure(PromotionsErrors.AlreadyInactive);
        IsActive = false;
        return Result.Success();
    }

    public bool IsRunning(DateTime utcNow) => IsActive && utcNow >= StartsAt && utcNow <= EndsAt;
}
