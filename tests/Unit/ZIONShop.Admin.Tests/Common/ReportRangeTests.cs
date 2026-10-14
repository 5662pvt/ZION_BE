using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Tests.TestSupport;

namespace ZIONShop.Admin.Tests.Common;

public class ReportRangeTests
{
    private static readonly FakeTimeProvider Now = new(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_Should_DefaultToLast30DaysInclToday_When_FromAndToAreMissing()
    {
        var result = ReportRange.Create(null, null, Now);

        result.IsSuccess.Should().BeTrue();
        var range = result.Value;

        // "now" is 2026-10-13T04:00Z == 2026-10-13T11:00+07, so VN-local today is 2026-10-13.
        range.To.Should().Be(new DateOnly(2026, 10, 13));
        range.From.Should().Be(new DateOnly(2026, 9, 14));
        range.Buckets.Should().HaveCount(30);
    }

    [Fact]
    public void Create_Should_PutCompletedAtNearUtcMidnight_Into_VietnamLocalDayBucket()
    {
        var result = ReportRange.Create("2026-10-10", "2026-10-15", Now);
        result.IsSuccess.Should().BeTrue();
        var range = result.Value;

        // 2026-10-12T17:30Z + 7h = 2026-10-13T00:30 local -> belongs to the "2026-10-13" VN-day bucket.
        var instant = new DateTime(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc);

        var bucket = range.Buckets.Single(b => instant >= b.StartUtc && instant < b.EndUtcExclusive);
        bucket.Label.Should().Be("2026-10-13");
    }

    [Fact]
    public void Create_Should_UseMonthGranularity_And_ZeroFillEveryMonth_When_RangeExceeds92Days()
    {
        var result = ReportRange.Create("2026-01-01", "2026-10-13", Now);
        result.IsSuccess.Should().BeTrue();
        var range = result.Value;

        range.Granularity.Should().Be(ReportGranularity.Month);
        range.Buckets.Select(b => b.Label).Should().BeEquivalentTo(
            "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06",
            "2026-07", "2026-08", "2026-09", "2026-10");
    }

    [Fact]
    public void Create_Should_Fail_When_DateFormatIsInvalid()
    {
        var result = ReportRange.Create("2026-13-40", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.InvalidDateFormat");
    }

    [Fact]
    public void Create_Should_Fail_When_FromIsAfterTo()
    {
        var result = ReportRange.Create("2026-10-15", "2026-10-10", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.InvalidRange");
    }

    [Fact]
    public void Create_Should_Fail_When_RangeExceeds366Days()
    {
        var result = ReportRange.Create("2025-01-01", "2026-06-01", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.RangeTooLarge");
    }
}
