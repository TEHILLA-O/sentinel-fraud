using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sentinel.SharedKernel;

public static class SentinelTelemetry
{
    public const string MeterName = "Sentinel";
    public const string ActivitySourceName = "Sentinel";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public static readonly Counter<long> TransactionsProcessed =
        Meter.CreateCounter<long>("sentinel_transactions_processed_total");

    public static readonly Counter<long> TransactionsBlocked =
        Meter.CreateCounter<long>("sentinel_transactions_blocked_total");

    public static readonly Counter<long> TransactionsReview =
        Meter.CreateCounter<long>("sentinel_transactions_review_total");

    public static readonly Histogram<double> RiskProcessingDuration =
        Meter.CreateHistogram<double>("sentinel_risk_processing_duration", unit: "ms");

    public static readonly UpDownCounter<long> CasesOpen =
        Meter.CreateUpDownCounter<long>("sentinel_cases_open");

    public static readonly Counter<long> RuleTrigger =
        Meter.CreateCounter<long>("sentinel_rule_trigger_total");

    public static readonly Counter<long> ProcessingErrors =
        Meter.CreateCounter<long>("sentinel_processing_errors_total");

    public static readonly Counter<long> DeadLetter =
        Meter.CreateCounter<long>("sentinel_deadletter_total");
}

public sealed class LatencyTracker
{
    private readonly long[] _samples;
    private readonly object _gate = new();
    private int _count;
    private int _index;

    public LatencyTracker(int capacity = 2048)
    {
        _samples = new long[capacity];
    }

    public void Record(long milliseconds)
    {
        lock (_gate)
        {
            _samples[_index] = milliseconds;
            _index = (_index + 1) % _samples.Length;
            if (_count < _samples.Length)
            {
                _count++;
            }
        }
    }

    public LatencySnapshot Snapshot()
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                return new LatencySnapshot(0, 0, 0, 0);
            }

            var copy = new long[_count];
            Array.Copy(_samples, copy, _count);
            Array.Sort(copy);
            return new LatencySnapshot(
                Average: copy.Average(),
                P50: Percentile(copy, 0.50),
                P95: Percentile(copy, 0.95),
                P99: Percentile(copy, 0.99));
        }
    }

    private static long Percentile(long[] sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }
}

public sealed record LatencySnapshot(double Average, long P50, long P95, long P99);
