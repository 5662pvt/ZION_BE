using System.Globalization;
using ZIONShop.Admin.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Common;

public enum ReportGranularity
{
    Day,
    Month
}

public sealed record ReportBucket(string Label, DateTime StartUtc, DateTime EndUtcExclusive);

public sealed class ReportRange
{
    public const int MaxDays = 366;
    private const int DayGranularityThreshold = 92;
    private const int DefaultRangeDays = 30;

    public static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private ReportRange(
        DateOnly from, DateOnly to,
        DateTime fromUtc, DateTime toUtc,
        DateTime previousFromUtc, DateTime previousToUtc,
        ReportGranularity granularity, IReadOnlyList<ReportBucket> buckets)
    {
        From = from;
        To = to;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        PreviousFromUtc = previousFromUtc;
        PreviousToUtc = previousToUtc;
        Granularity = granularity;
        Buckets = buckets;
    }

    public DateOnly From { get; }
    public DateOnly To { get; }
    public DateTime FromUtc { get; }
    public DateTime ToUtc { get; }
    public DateTime PreviousFromUtc { get; }
    public DateTime PreviousToUtc { get; }
    public ReportGranularity Granularity { get; }
    public IReadOnlyList<ReportBucket> Buckets { get; }

    public static Result<ReportRange> Create(string? from, string? to, TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTimeZone).Date);

        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);

        DateOnly fromDate;
        DateOnly toDate;

        if (!hasFrom && !hasTo)
        {
            toDate = today;
            fromDate = today.AddDays(-(DefaultRangeDays - 1));
        }
        else
        {
            if (hasTo)
            {
                if (!TryParseDate(to!, out toDate)) return Result.Failure<ReportRange>(AdminErrors.InvalidDateFormat);
            }
            else
            {
                toDate = today;
            }

            if (hasFrom)
            {
                if (!TryParseDate(from!, out fromDate)) return Result.Failure<ReportRange>(AdminErrors.InvalidDateFormat);
            }
            else
            {
                fromDate = toDate.AddDays(-(DefaultRangeDays - 1));
            }
        }

        if (fromDate > toDate) return Result.Failure<ReportRange>(AdminErrors.InvalidRange);

        var days = toDate.DayNumber - fromDate.DayNumber + 1;
        if (days > MaxDays) return Result.Failure<ReportRange>(AdminErrors.RangeTooLarge);

        var previousTo = fromDate.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(days - 1));

        var fromUtc = ToUtcMidnight(fromDate);
        var toUtc = ToUtcMidnight(toDate.AddDays(1));
        var previousFromUtc = ToUtcMidnight(previousFrom);
        var previousToUtc = ToUtcMidnight(previousTo.AddDays(1));

        var granularity = days <= DayGranularityThreshold ? ReportGranularity.Day : ReportGranularity.Month;
        var buckets = BuildBuckets(fromDate, toDate, granularity);

        return Result.Success(new ReportRange(fromDate, toDate, fromUtc, toUtc, previousFromUtc, previousToUtc, granularity, buckets));
    }

    private static bool TryParseDate(string value, out DateOnly date)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static DateTime ToUtcMidnight(DateOnly date)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, VietnamTimeZone);
    }

    private static IReadOnlyList<ReportBucket> BuildBuckets(DateOnly from, DateOnly to, ReportGranularity granularity)
    {
        var buckets = new List<ReportBucket>();

        if (granularity == ReportGranularity.Day)
        {
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                buckets.Add(new ReportBucket(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ToUtcMidnight(d), ToUtcMidnight(d.AddDays(1))));
            }
        }
        else
        {
            var cursor = new DateOnly(from.Year, from.Month, 1);
            var end = new DateOnly(to.Year, to.Month, 1);
            while (cursor <= end)
            {
                var next = cursor.AddMonths(1);
                buckets.Add(new ReportBucket(cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture), ToUtcMidnight(cursor), ToUtcMidnight(next)));
                cursor = next;
            }
        }

        return buckets;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("VN-Fixed", TimeSpan.FromHours(7), "Vietnam Time (fixed +07:00)", "Vietnam Time");
    }
}
