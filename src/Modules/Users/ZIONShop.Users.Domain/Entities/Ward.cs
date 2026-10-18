using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class Ward : BaseEntity
{
    private Ward() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid ProvinceId { get; private set; }

    public static Ward Create(string code, string name, Guid provinceId) => new()
    {
        Id = Guid.NewGuid(),
        Code = code.Trim(),
        Name = name.Trim(),
        ProvinceId = provinceId
    };
}
