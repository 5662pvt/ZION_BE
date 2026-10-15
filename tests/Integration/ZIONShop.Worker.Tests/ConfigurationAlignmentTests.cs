using Microsoft.Extensions.Configuration;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.Worker.Tests;

/// <summary>
/// Regression test for the Worker/API queue mismatch: the Worker's own appsettings.json used to pin
/// EventBus:QueueName to "zionshop.worker" and EventBus:Provider to "InMemory", leaving the
/// API-declared queue (RabbitMqOptions.QueueName's default, "zionshop.api") with zero consumers while
/// the Worker drained a queue of its own -- every event got processed twice (once per queue) whenever
/// the API's own BackgroundProcessing happened to be on, and piled up forever in the orphaned API
/// queue when it was off. Loads only appsettings.json + appsettings.Development.json for each host
/// (no process environment variables), matching how a plain `dotnet run` resolves configuration
/// locally without docker-compose's env overrides.
/// </summary>
public class ConfigurationAlignmentTests
{
    private const string WorkerProjectRelativePath = "src/Worker/ZIONShop.Worker";
    private const string ApiProjectRelativePath = "src/Api/ZIONShop.Api";

    private static RabbitMqOptions LoadRabbitMqOptions(string hostProjectRelativePath)
    {
        var hostDirectory = Path.Combine(FindRepoRoot(), hostProjectRelativePath);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(hostDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(hostDirectory, "appsettings.Development.json"), optional: false)
            .Build();

        return configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException($"Could not bind '{RabbitMqOptions.SectionName}' from {hostDirectory}.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ZIONShop.slnx")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException($"Could not locate ZIONShop.slnx above '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void Worker_And_Api_Should_ResolveTheSameQueueAndExchange_WithNoEnvironmentOverride()
    {
        var workerOptions = LoadRabbitMqOptions(WorkerProjectRelativePath);
        var apiOptions = LoadRabbitMqOptions(ApiProjectRelativePath);

        workerOptions.QueueName.Should().Be(apiOptions.QueueName);
        workerOptions.Exchange.Should().Be(apiOptions.Exchange);
    }

    [Fact]
    public void Worker_Should_DefaultToRabbitMqProvider_WithNoEnvironmentOverride()
    {
        var workerOptions = LoadRabbitMqOptions(WorkerProjectRelativePath);

        // A separate-process worker that only exists to consume/publish is meaningless under the
        // InMemory provider -- unlike the API (which legitimately defaults to InMemory for the
        // single-process dev run), the Worker must default to the real broker.
        workerOptions.Provider.Should().Be("RabbitMq");
    }
}
