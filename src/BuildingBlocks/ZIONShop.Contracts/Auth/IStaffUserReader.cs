namespace ZIONShop.Contracts.Auth;

public sealed record StaffUser(Guid UserId);

public interface IStaffUserReader
{
    Task<IReadOnlyList<StaffUser>> GetAdminAndStaffUserIdsAsync(CancellationToken ct = default);
}
