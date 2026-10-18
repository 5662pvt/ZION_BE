using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Domain.Repositories;

public interface IAdministrativeUnitRepository
{
    Task<IReadOnlyList<Province>> GetProvincesAsync(CancellationToken cancellationToken = default);
    Task<Province?> GetProvinceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Province?> FindProvinceByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ward>> GetWardsByProvinceIdAsync(Guid provinceId, CancellationToken cancellationToken = default);
    Task<Ward?> FindWardByNameAsync(Guid provinceId, string name, CancellationToken cancellationToken = default);
}
