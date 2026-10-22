using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Application.Mappings;

public static class LoyaltyMappingExtensions
{
    public static LoyaltyTransactionDto ToDto(this PointTransaction transaction) =>
        new(transaction.Id, transaction.Type.ToString(), transaction.Points, transaction.BalanceAfter, transaction.OrderId, transaction.Description, transaction.CreatedAt);

    public static LoyaltyLotDto ToDto(this PointLot lot) =>
        new(lot.Id, lot.Source.ToString(), lot.Original, lot.Remaining, lot.EarnedAt, lot.ExpiresAt);
}
