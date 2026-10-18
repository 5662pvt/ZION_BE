using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Infrastructure.Persistence;

public static class AdministrativeUnitsSeeder
{
    public static async Task SeedAsync(UsersDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Provinces.AnyAsync(cancellationToken)) return;

        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "vn-administrative-units.json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var provincesJson = JsonSerializer.Deserialize<List<ProvinceJson>>(json) ?? [];

        var provinces = new List<Province>(provincesJson.Count);
        var wards = new List<Ward>();
        foreach (var p in provincesJson)
        {
            var province = Province.Create(p.Code, p.FullName);
            provinces.Add(province);
            foreach (var w in p.Wards)
                wards.Add(Ward.Create(w.Code, w.FullName, province.Id));
        }

        await db.Provinces.AddRangeAsync(provinces, cancellationToken);
        await db.Wards.AddRangeAsync(wards, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ProvinceJson(string Code, string FullName, List<WardJson> Wards);

    private sealed record WardJson(string Code, string FullName);
}
