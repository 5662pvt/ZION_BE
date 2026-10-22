namespace ZIONShop.Loyalty.Application.DTOs;

public record LoyaltySummaryDto(int Balance, int ExpiringSoonPoints, DateTime? ExpiringSoonDate, int MaxRedeemPercent, int EarnRatePercent, int ReviewBonus);

public record LoyaltyTransactionDto(Guid Id, string Type, int Points, int BalanceAfter, Guid? OrderId, string Description, DateTime CreatedAt);

public record LoyaltyLotDto(Guid Id, string Source, int Original, int Remaining, DateTime EarnedAt, DateTime ExpiresAt);

public record CustomerLoyaltyDto(LoyaltySummaryDto Summary, IReadOnlyList<LoyaltyTransactionDto> RecentTransactions, IReadOnlyList<LoyaltyLotDto> ActiveLots);
