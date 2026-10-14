using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Infrastructure.Storage;

namespace ZIONShop.Products.Tests.Storage;

public class UploadsRootProviderTests
{
    private static Mock<IHostEnvironment> MakeEnv(string contentRootPath)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(contentRootPath);
        return env;
    }

    [Fact]
    public void RootPath_Should_DefaultToAppDataUploadsUnderContentRoot_When_LocalRootPathEmpty()
    {
        var env = MakeEnv(Path.Combine("C:", "app"));
        var options = Options.Create(new ImageStorageOptions { LocalRootPath = null });

        var provider = new UploadsRootProvider(env.Object, options);

        provider.RootPath.Should().Be(Path.Combine("C:", "app", "App_Data", "uploads"));
    }

    [Fact]
    public void RootPath_Should_UseConfiguredAbsolutePath_When_LocalRootPathIsAbsolute()
    {
        var env = MakeEnv(Path.Combine("C:", "app"));
        var absolute = Path.Combine("D:", "data", "uploads");
        var options = Options.Create(new ImageStorageOptions { LocalRootPath = absolute });

        var provider = new UploadsRootProvider(env.Object, options);

        provider.RootPath.Should().Be(absolute);
    }

    [Fact]
    public void RootPath_Should_ResolveRelativeToContentRoot_When_LocalRootPathIsRelative()
    {
        var env = MakeEnv(Path.Combine("C:", "app"));
        var options = Options.Create(new ImageStorageOptions { LocalRootPath = Path.Combine("data", "uploads") });

        var provider = new UploadsRootProvider(env.Object, options);

        provider.RootPath.Should().Be(Path.Combine("C:", "app", "data", "uploads"));
    }
}
