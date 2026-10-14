using Microsoft.Extensions.DependencyInjection;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.Tests.Registry;

public class IntegrationEventHandlerRegistrationTests
{
    [Fact]
    public void AddIntegrationEventHandler_Should_RegisterHandlerAndCollectEventType_When_Called()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEventHandler<SampleIntegrationEvent, SampleIntegrationEventHandler>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IntegrationEventTypeRegistry>();

        registry.RegisteredTypes.Should().ContainSingle().Which.Should().Be(typeof(SampleIntegrationEvent));
        provider.GetServices<IIntegrationEventHandler<SampleIntegrationEvent>>()
            .Should().ContainSingle(h => h is SampleIntegrationEventHandler);
    }

    [Fact]
    public void AddIntegrationEventHandler_Should_ShareOneRegistry_When_CalledForMultipleEvents()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEventHandler<SampleIntegrationEvent, SampleIntegrationEventHandler>();
        services.AddIntegrationEventHandler<OtherIntegrationEvent, OtherIntegrationEventHandler>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IntegrationEventTypeRegistry>();

        registry.RegisteredTypes.Should().BeEquivalentTo(new[] { typeof(SampleIntegrationEvent), typeof(OtherIntegrationEvent) });
        registry.TryGetType(nameof(SampleIntegrationEvent), out var resolved).Should().BeTrue();
        resolved.Should().Be(typeof(SampleIntegrationEvent));
    }
}
