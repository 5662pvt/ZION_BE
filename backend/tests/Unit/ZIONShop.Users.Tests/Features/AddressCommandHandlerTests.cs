using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Application.Features.AddAddress;
using ZIONShop.Users.Application.Features.RemoveAddress;
using ZIONShop.Users.Application.Features.SetDefaultAddress;
using ZIONShop.Users.Application.Features.UpdateAddress;
using ZIONShop.Users.Application.Mappings;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Repositories;

namespace ZIONShop.Users.Tests.Features;

public class AddressCommandHandlerTests
{
    [Fact]
    public async Task AddAddress_Should_SetFirstAddressAsDefault_When_IsDefaultFalse()
    {
        var (db, profiles, mapper) = BuildScope();
        var profile = UserProfile.Create(Guid.NewGuid(), "user@zionshop.local", "User");
        await db.UserProfiles.AddAsync(profile);
        await db.SaveChangesAsync();

        var handler = new AddAddressCommandHandler(profiles, db, mapper);

        var result = await handler.Handle(new AddAddressCommand(profile.AuthUserId, "1 Main", null, "HCMC", null, "VN", "70000", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAddress_Should_UpdateAddressAndMakeItDefault_When_IsDefaultTrue()
    {
        var (db, profiles, mapper) = BuildScope();
        var profile = UserProfile.Create(Guid.NewGuid(), "user@zionshop.local", "User");
        var first = profile.AddAddress("1 Main", null, "HCMC", null, "VN", "70000", true);
        var second = profile.AddAddress("2 Main", null, "Hanoi", null, "VN", "10000", false);
        await db.UserProfiles.AddAsync(profile);
        await db.SaveChangesAsync();

        var handler = new UpdateAddressCommandHandler(profiles, db, mapper);

        var result = await handler.Handle(new UpdateAddressCommand(profile.AuthUserId, second.Id, "2 Updated", "Apt 1", "Da Nang", "DN", "VN", "55000", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Line1.Should().Be("2 Updated");
        result.Value.IsDefault.Should().BeTrue();

        var saved = await db.UserProfiles.Include(p => p.Addresses).SingleAsync(p => p.Id == profile.Id);
        saved.Addresses.Single(a => a.Id == first.Id).IsDefault.Should().BeFalse();
        saved.Addresses.Single(a => a.Id == second.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAddress_Should_UnsetOtherDefaults_When_AddressExists()
    {
        var (db, profiles, mapper) = BuildScope();
        var profile = UserProfile.Create(Guid.NewGuid(), "user@zionshop.local", "User");
        var first = profile.AddAddress("1 Main", null, "HCMC", null, "VN", "70000", true);
        var second = profile.AddAddress("2 Main", null, "Hanoi", null, "VN", "10000", false);
        await db.UserProfiles.AddAsync(profile);
        await db.SaveChangesAsync();

        var handler = new SetDefaultAddressCommandHandler(profiles, db, mapper);

        var result = await handler.Handle(new SetDefaultAddressCommand(profile.AuthUserId, second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(second.Id);
        result.Value.IsDefault.Should().BeTrue();

        var saved = await db.UserProfiles.Include(p => p.Addresses).SingleAsync(p => p.Id == profile.Id);
        saved.Addresses.Single(a => a.Id == first.Id).IsDefault.Should().BeFalse();
        saved.Addresses.Single(a => a.Id == second.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAddress_Should_RemoveAddressAndPromoteAnotherDefault_When_RemovedAddressWasDefault()
    {
        var (db, profiles, mapper) = BuildScope();
        var profile = UserProfile.Create(Guid.NewGuid(), "user@zionshop.local", "User");
        var first = profile.AddAddress("1 Main", null, "HCMC", null, "VN", "70000", true);
        var second = profile.AddAddress("2 Main", null, "Hanoi", null, "VN", "10000", false);
        await db.UserProfiles.AddAsync(profile);
        await db.SaveChangesAsync();

        var handler = new RemoveAddressCommandHandler(profiles, db);

        var result = await handler.Handle(new RemoveAddressCommand(profile.AuthUserId, first.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var saved = await db.UserProfiles.Include(p => p.Addresses).SingleAsync(p => p.Id == profile.Id);
        saved.Addresses.Should().ContainSingle();
        saved.Addresses.Single().Id.Should().Be(second.Id);
        saved.Addresses.Single().IsDefault.Should().BeTrue();
    }

    private static (UsersDbContext db, UserProfileRepository profiles, IMapper mapper) BuildScope()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new UsersDbContext(options);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<UsersMappingProfile>()).CreateMapper();

        return (db, new UserProfileRepository(db), mapper);
    }
}
