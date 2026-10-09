using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// Minimal hand-rolled <see cref="IServiceScopeFactory"/> so handler tests can exercise the real
/// "create a fresh scope per retry attempt" code path without pulling in a full DI container.
/// Each scope gets a freshly-built <see cref="IServiceProvider"/> from the supplied factory.
/// </summary>
internal sealed class SimpleServiceScopeFactory : IServiceScopeFactory
{
    private readonly Func<IServiceProvider> _providerFactory;

    public SimpleServiceScopeFactory(Func<IServiceProvider> providerFactory) => _providerFactory = providerFactory;

    public IServiceScope CreateScope() => new SimpleServiceScope(_providerFactory());

    private sealed class SimpleServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public SimpleServiceScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;
        public void Dispose() { }
    }
}

internal sealed class DictionaryServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, Func<object>> _factories = new();

    public DictionaryServiceProvider Add<TService>(Func<TService> factory) where TService : notnull
    {
        _factories[typeof(TService)] = () => factory();
        return this;
    }

    public object? GetService(Type serviceType) => _factories.TryGetValue(serviceType, out var factory) ? factory() : null;
}
