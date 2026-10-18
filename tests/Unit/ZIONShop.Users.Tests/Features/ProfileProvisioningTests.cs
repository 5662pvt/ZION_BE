using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Auth;
using ZIONShop.Users.Application.Features.AddAddress;
using ZIONShop.Users.Application.Features.GetProfile;
using ZIONShop.Users.Application.Features.ListAddresses;
using ZIONShop.Users.Application.Features.UpdateProfile;
using ZIONShop.Users.Application.IntegrationEventHandlers;
using ZIONShop.Users.Application.Mappings;
using ZIONShop.Users.Application.Services;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Repositories;

namespace ZIONShop.Users.Tests.Features;

public class ProfileProvisioningTests
{
    [Fact]
    public async Task EnsureAsync_Should_CreateProfile_When_NoneExistsForAuthUser()
    {
        var (db, profiles, provisioner, _) = BuildScope();
        var authUserId = Guid.NewGuid();

        var profile = await provisioner.EnsureAsync(authUserId, "admin@zionshop.local", "ZIONShop Admin", CancellationToken.None);

        profile.AuthUserId.Should().Be(authUserId);
        profile.Email.Should().Be("admin@zionshop.local");
        (await db.UserProfiles.CountAsync(p => p.AuthUserId == authUserId)).Should().Be(1);
    }

    [Fact]
    public async Task EnsureAsync_Should_ReturnExistingProfile_When_CalledTwice()
    {
        var (db, profiles, provisioner, _) = BuildScope();
        var authUserId = Guid.NewGuid();

        var first = await provisioner.EnsureAsync(authUserId, "admin@zionshop.local", "ZIONShop Admin", CancellationToken.None);
        var second = await provisioner.EnsureAsync(authUserId, "admin@zionshop.local", "ZIONShop Admin", CancellationToken.None);

        second.Id.Should().Be(first.Id);
        (await db.UserProfiles.CountAsync(p => p.AuthUserId == authUserId)).Should().Be(1);
    }

    [Fact]
    public async Task GetProfile_Should_ProvisionProfile_When_SeededAdminHasNoProfileYet()
    {
        var (_, _, _, handler) = BuildHandlers();
        var authUserId = Guid.NewGuid();

        var result = await handler.getProfile.Handle(new GetUserProfileQuery(authUserId, "admin@zionshop.local"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("admin@zionshop.local");
    }

    [Fact]
    public async Task UpdateProfile_Should_ProvisionThenUpdate_When_ProfileMissing()
    {
        var (_, _, _, handler) = BuildHandlers();
        var authUserId = Guid.NewGuid();

        var result = await handler.updateProfile.Handle(
            new UpdateUserProfileCommand(authUserId, "admin@zionshop.local", "Admin Name", "0901234567", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Admin Name");
    }

    [Fact]
    public async Task ListAddresses_Should_ProvisionProfile_And_ReturnEmptyList_When_ProfileMissing()
    {
        var (_, _, _, handler) = BuildHandlers();
        var authUserId = Guid.NewGuid();

        var result = await handler.listAddresses.Handle(new ListAddressesQuery(authUserId, "admin@zionshop.local"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAddress_Should_ProvisionProfile_When_ProfileMissing()
    {
        var (_, _, _, handler) = BuildHandlers();
        var authUserId = Guid.NewGuid();

        var result = await handler.addAddress.Handle(
            new AddAddressCommand(authUserId, "admin@zionshop.local", "Nguyen Van A", "0901234567", "TP.HCM", "Phuong Ben Nghe", "123 Main St", false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UserRegisteredHandler_Should_BeIdempotent_When_EventHandledTwice()
    {
        var (db, profiles, provisioner, _) = BuildScope();
        var handler = new UserRegisteredIntegrationEventHandler(provisioner);
        var @event = new UserRegisteredIntegrationEvent(Guid.NewGuid(), "new-user@zionshop.local", "New User");

        await handler.HandleAsync(@event, CancellationToken.None);
        await handler.HandleAsync(@event, CancellationToken.None);

        (await db.UserProfiles.CountAsync(p => p.AuthUserId == @event.UserId)).Should().Be(1);
    }

    private static (UsersDbContext db, UserProfileRepository profiles, IUserProfileProvisioner provisioner, IMapper mapper) BuildScope()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new UsersDbContext(options);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<UsersMappingProfile>()).CreateMapper();
        var profiles = new UserProfileRepository(db);

        var hcm = Province.Create("79", "TP.HCM");
        db.Provinces.Add(hcm);
        db.Wards.Add(Ward.Create("001", "Phuong Ben Nghe", hcm.Id));
        db.SaveChanges();

        return (db, profiles, new UserProfileProvisioner(profiles, db), mapper);
    }

    private static (UsersDbContext db, UserProfileRepository profiles, IUserProfileProvisioner provisioner,
        (GetUserProfileQueryHandler getProfile, UpdateUserProfileCommandHandler updateProfile, ListAddressesQueryHandler listAddresses, AddAddressCommandHandler addAddress) handlers) BuildHandlers()
    {
        var (db, profiles, provisioner, mapper) = BuildScope();
        var administrativeUnits = new AdministrativeUnitRepository(db);
        var handlers = (
            getProfile: new GetUserProfileQueryHandler(provisioner, mapper),
            updateProfile: new UpdateUserProfileCommandHandler(provisioner, profiles, db, mapper),
            listAddresses: new ListAddressesQueryHandler(provisioner, mapper),
            addAddress: new AddAddressCommandHandler(provisioner, profiles, administrativeUnits, db, mapper));

        return (db, profiles, provisioner, handlers);
    }
}
