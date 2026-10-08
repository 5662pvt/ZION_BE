using System.Collections.Concurrent;

namespace ZIONShop.EventBus.Registry;

public class IntegrationEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _typesByRoutingKey = new();

    public void Register(Type eventType) => _typesByRoutingKey.TryAdd(eventType.Name, eventType);

    public bool TryGetType(string routingKey, out Type eventType) =>
        _typesByRoutingKey.TryGetValue(routingKey, out eventType!);

    public IReadOnlyCollection<Type> RegisteredTypes => _typesByRoutingKey.Values.ToList();
}
