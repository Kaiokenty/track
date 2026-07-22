using Track.Core.Models;

namespace Track.Core.Pace;

/// <summary>
/// Recommended burn = 100% × (elapsed / window). Compare actual cumulative usage to that line.
/// </summary>
public static class PaceCalculator
{
    private const double OnTrackTolerancePercent = 5;

    public static PaceSnapshot Calculate(UsageMeter meter, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var actual = meter.PercentUsed;
        var recommended = RecommendedPercent(meter, at);
        var delta = actual - recommended;
        var verdict = delta switch
        {
            > OnTrackTolerancePercent => PaceVerdict.Over,
            < -OnTrackTolerancePercent => PaceVerdict.Under,
            _ => PaceVerdict.OnTrack
        };

        return new PaceSnapshot
        {
            Meter = meter,
            RecommendedPercent = recommended,
            ActualPercent = actual,
            Verdict = verdict,
            ProjectedExhaustion = ProjectExhaustion(meter, at)
        };
    }

    public static double RecommendedPercent(UsageMeter meter, DateTimeOffset now)
    {
        if (meter.Start is null || meter.End is null || meter.End <= meter.Start)
            return EvenSplitFallback(meter.Kind, now);

        var total = (meter.End.Value - meter.Start.Value).TotalSeconds;
        if (total <= 0) return 0;

        var elapsed = (now - meter.Start.Value).TotalSeconds;
        return Math.Clamp(elapsed / total * 100, 0, 100);
    }

    private static double EvenSplitFallback(MeterKind kind, DateTimeOffset now) =>
        kind switch
        {
            MeterKind.Weekly => now.DayOfWeek switch
            {
                DayOfWeek.Sunday => 100.0 / 7,
                var d => ((int)d) / 7.0 * 100
            },
            MeterKind.Rolling5h => 50, // midpoint guess without window bounds
            _ => now.Day / (double)DateTime.DaysInMonth(now.Year, now.Month) * 100
        };

    private static DateTimeOffset? ProjectExhaustion(UsageMeter meter, DateTimeOffset now)
    {
        if (meter.Start is null || meter.Used <= 0 || meter.Limit <= 0)
            return null;

        var elapsed = now - meter.Start.Value;
        if (elapsed.TotalSeconds <= 0) return null;

        var ratePerSecond = meter.Used / elapsed.TotalSeconds;
        if (ratePerSecond <= 0) return null;

        var secondsToLimit = (meter.Limit - meter.Used) / ratePerSecond;
        if (secondsToLimit < 0) return now;

        return now.AddSeconds(secondsToLimit);
    }
}
