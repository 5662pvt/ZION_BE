using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.EventBus.Diagnostics;
using ZIONShop.EventBus.Inbox;

namespace ZIONShop.EventBus.Tests.Diagnostics;

public class MessagingDiagnosticsTests
{
    private const string TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
    private const string TraceState = "vendor=value";

    [Fact]
    public void ExtractParentContext_Should_ParseTraceContext_When_HeaderValuesAreStrings()
    {
        var headers = new Dictionary<string, object>
        {
            ["traceparent"] = TraceParent,
            ["tracestate"] = TraceState
        };

        var context = MessagingDiagnostics.ExtractParentContext(headers);

        context.TraceId.ToString().Should().Be("0af7651916cd43dd8448eb211c80319c");
        context.SpanId.ToString().Should().Be("b7ad6b7169203331");
        context.TraceState.Should().Be(TraceState);
    }

    [Fact]
    public void ExtractParentContext_Should_ParseTraceContext_When_HeaderValuesAreByteArrays()
    {
        var headers = new Dictionary<string, object>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes(TraceParent),
            ["tracestate"] = Encoding.UTF8.GetBytes(TraceState)
        };

        var context = MessagingDiagnostics.ExtractParentContext(headers);

        context.TraceId.ToString().Should().Be("0af7651916cd43dd8448eb211c80319c");
        context.SpanId.ToString().Should().Be("b7ad6b7169203331");
        context.TraceState.Should().Be(TraceState);
    }

    [Fact]
    public void ExtractParentContext_Should_ReturnDefault_When_HeadersAreNull()
    {
        MessagingDiagnostics.ExtractParentContext(null).Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractParentContext_Should_ReturnDefault_When_TraceParentHeaderIsMissing()
    {
        var headers = new Dictionary<string, object> { ["x-retry-count"] = 1 };

        MessagingDiagnostics.ExtractParentContext(headers).Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractParentContext_Should_ReturnDefault_When_TraceParentIsMalformed()
    {
        var headers = new Dictionary<string, object> { ["traceparent"] = "not-a-traceparent" };

        MessagingDiagnostics.ExtractParentContext(headers).Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ResolveHandlerName_Should_ReturnPlainTypeName_When_HandlerIsNotDecorated()
    {
        var handler = new SampleIntegrationEventHandler();

        MessagingDiagnostics.ResolveHandlerName(handler).Should().Be(nameof(SampleIntegrationEventHandler));
    }

    [Fact]
    public void ResolveHandlerName_Should_ReturnTheInnerHandlersName_When_HandlerIsTheIdempotentInboxDecorator()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new TestDbContext(options);
        var decorated = new IdempotentIntegrationEventHandler<SampleIntegrationEvent, SampleIntegrationEventHandler, TestDbContext>(
            new SampleIntegrationEventHandler(),
            db,
            NullLogger<IdempotentIntegrationEventHandler<SampleIntegrationEvent, SampleIntegrationEventHandler, TestDbContext>>.Instance);

        var name = MessagingDiagnostics.ResolveHandlerName(decorated);

        // The decorator's own type name (IdempotentIntegrationEventHandler`3) would hide which
        // consumer actually ran -- this must resolve to the wrapped handler, qualified by its module.
        name.Should().NotContain("IdempotentIntegrationEventHandler");
        name.Should().Be($"EventBus.{nameof(SampleIntegrationEventHandler)}");
    }
}
