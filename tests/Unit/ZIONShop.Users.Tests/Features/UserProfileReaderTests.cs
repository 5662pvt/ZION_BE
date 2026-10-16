using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Readers;

namespace ZIONShop.Users.Tests.Features;

public class UserProfileReaderTests
{
    [Fact]
    public async Task GetProfilesAsync_Should_ReturnPhoneAndAddressesCount_When_ProfilesExist()
    {
        var db = BuildDbContext();
        var profile = UserProfile.Create(Guid.NewGuid(), "user@zionshop.local", "Nguyen Van A");
        profile.UpdateProfile("Nguyen Van A", "0901234567", null);
        profile.AddAddress("Nguyen Van A", "0901234567", "TP.HCM", "Phuong Ben Nghe", "123 Main St", true);
        profile.AddAddress("Nguyen Van A", "0901234567", "Ha Noi", "Phuong Hoan Kiem", "2 Main St", false);
        await db.UserProfiles.AddAsync(profile);
        await db.SaveChangesAsync();

        var reader = new UserProfileReader(db);

        var result = await reader.GetProfilesAsync(new[] { profile.AuthUserId });

        var snapshot = result.Single();
        snapshot.PhoneNumber.Should().Be("0901234567");
        snapshot.AddressesCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfilesAsync_Should_ReturnEmpty_When_NoIdsRequested()
    {
        var db = BuildDbContext();
        var reader = new UserProfileReader(db);

        var result = await reader.GetProfilesAsync(Array.Empty<Guid>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAuthUserIdsByPhoneAsync_Should_ReturnMatchingAuthUserIds_When_PhoneContainsKeyword()
    {
        var db = BuildDbContext();
        var match = UserProfile.Create(Guid.NewGuid(), "match@zionshop.local");
        match.UpdateProfile(null, "0901234567", null);
        var other = UserProfile.Create(Guid.NewGuid(), "other@zionshop.local");
        other.UpdateProfile(null, "0987654321", null);
        await db.UserProfiles.AddRangeAsync(match, other);
        await db.SaveChangesAsync();

        var reader = new UserProfileReader(db);

        var result = await reader.FindAuthUserIdsByPhoneAsync("901234");

        result.Should().ContainSingle().Which.Should().Be(match.AuthUserId);
    }

    private static UsersDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new UsersDbContext(options);
    }
}
