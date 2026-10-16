using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;
using ZIONShop.Caching.DependencyInjection;

namespace ZIONShop.Caching.IntegrationTests;

/// <summary>Exercises <see cref="CachingExtensions.AddZionCaching"/> purely at the
/// <see cref="IServiceCollection"/> level (no real DI container/Redis needed) -- this project has no
/// reference to the concrete <c>Microsoft.Extensions.DependencyInjection</c>/<c>Configuration</c>
/// packages, only their Abstractions, which is enough since <see cref="IServiceCollection"/> is just
/// <c>IList&lt;ServiceDescriptor&gt;</c>.</summary>
public class CachingExtensionsToggleTests
{
    [Fact]
    public void AddZionCaching_Should_RegisterNullDistributedCacheService_When_CachingDisabled()
    {
        var services = new FakeServiceCollection();
        var configuration = new FakeConfiguration(new Dictionary<string, string?> { ["Caching:Enabled"] = "false" });

        services.AddZionCaching(configuration);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(IDistributedCacheService) &&
            d.ImplementationType == typeof(NullDistributedCacheService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddZionCaching_Should_NotRegisterConnectionMultiplexer_When_CachingDisabled()
    {
        var services = new FakeServiceCollection();
        var configuration = new FakeConfiguration(new Dictionary<string, string?> { ["Caching:Enabled"] = "false" });

        services.AddZionCaching(configuration);

        services.Should().NotContain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Fact]
    public void AddZionCaching_Should_NotThrow_When_CachingDisabledAndRedisUnavailable()
    {
        var services = new FakeServiceCollection();
        var configuration = new FakeConfiguration(new Dictionary<string, string?> { ["Caching:Enabled"] = "false" });

        var act = () => services.AddZionCaching(configuration);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddZionCaching_Should_RegisterRedisCacheService_When_CachingEnabledOrUnset()
    {
        var services = new FakeServiceCollection();
        var configuration = new FakeConfiguration(new Dictionary<string, string?>());

        services.AddZionCaching(configuration);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(IDistributedCacheService) &&
            d.ImplementationType == typeof(RedisCacheService));
        services.Should().Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    private sealed class FakeServiceCollection : List<ServiceDescriptor>, IServiceCollection
    {
    }

    private sealed class FakeConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public FakeConfiguration(Dictionary<string, string?> values) => _values = values;

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NullChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new FakeConfigurationSection(_values, key);
    }

    private sealed class FakeConfigurationSection : IConfigurationSection
    {
        private readonly Dictionary<string, string?> _values;

        public FakeConfigurationSection(Dictionary<string, string?> values, string path)
        {
            _values = values;
            Path = path;
            Key = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;
        }

        public string? this[string key]
        {
            get => _values.TryGetValue($"{Path}:{key}", out var value) ? value : null;
            set => _values[$"{Path}:{key}"] = value;
        }

        public string Key { get; }
        public string Path { get; }

        public string? Value
        {
            get => _values.TryGetValue(Path, out var value) ? value : null;
            set => _values[Path] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => NullChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new FakeConfigurationSection(_values, $"{Path}:{key}");
    }

    private sealed class NullChangeToken : IChangeToken
    {
        public static readonly NullChangeToken Instance = new();

        public bool ActiveChangeCallbacks => false;
        public bool HasChanged => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NoOpDisposable.Instance;

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
