using ZIONShop.Cart.Domain.Entities;

namespace ZIONShop.Cart.Domain.Repositories;

public interface ICartRepository
{
    Task<Entities.Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Entities.Cart?> GetByAnonymousIdAsync(string anonymousId, CancellationToken cancellationToken = default);
    Task<Entities.Cart?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.Cart cart, CancellationToken cancellationToken = default);
    void Update(Entities.Cart cart);
    void Remove(Entities.Cart cart);
}
