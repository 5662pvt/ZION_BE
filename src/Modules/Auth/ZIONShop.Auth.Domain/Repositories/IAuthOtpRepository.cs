using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Enums;

namespace ZIONShop.Auth.Domain.Repositories;

public interface IAuthOtpRepository
{
    Task<AuthOtp?> GetActiveByEmailAndPurposeAsync(string email, AuthOtpPurpose purpose, CancellationToken cancellationToken = default);
    Task InvalidateActiveByUserAndPurposeAsync(Guid userId, AuthOtpPurpose purpose, CancellationToken cancellationToken = default);
    Task AddAsync(AuthOtp otp, CancellationToken cancellationToken = default);
}
