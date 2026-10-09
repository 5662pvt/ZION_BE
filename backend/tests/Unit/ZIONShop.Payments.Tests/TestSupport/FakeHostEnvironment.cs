using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ZIONShop.Payments.Tests.TestSupport;

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "ZIONShop.Payments.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
