namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

public static class Eventually
{
    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(100);
        }

        condition().Should().BeTrue($"condition did not become true within {timeout}");
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(200);
        }

        (await condition()).Should().BeTrue($"condition did not become true within {timeout}");
    }
}
