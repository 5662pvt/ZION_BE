using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class Province : BaseEntity
{
    private Province() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public static Province Create(string code, string name) => new()
    {
        Id = Guid.NewGuid(),
        Code = code.Trim(),
        Name = name.Trim()
    };
}
