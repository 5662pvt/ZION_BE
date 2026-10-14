namespace ZIONShop.Contracts.Auth;

public interface IUserStatsReader
{
    /// <summary>Users with role Customer whose CreatedDate falls in [fromUtc, toUtc).</summary>
    Task<int> CountCustomersRegisteredAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
