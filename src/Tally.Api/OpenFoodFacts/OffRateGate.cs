using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Tally.Api.OpenFoodFacts;

/// <summary>
/// Instance-wide politeness for Open Food Facts: token buckets matching their
/// published per-IP limits, plus exponential back-off after errors.
/// </summary>
public sealed class OffRateGate : IDisposable
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private readonly TokenBucketRateLimiter _product;
    private readonly TokenBucketRateLimiter _search;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private DateTimeOffset _unavailableUntil = DateTimeOffset.MinValue;
    private int _consecutiveFailures;

    public OffRateGate(IOptions<TallyOptions> options, TimeProvider time)
    {
        _time = time;
        var o = options.Value.OpenFoodFacts;
        _product = Bucket(o.ProductRequestsPerMinute, queue: 4);
        _search = Bucket(o.SearchRequestsPerMinute, queue: 0);
    }

    private static TokenBucketRateLimiter Bucket(int perMinute, int queue)
    {
        perMinute = Math.Max(1, perMinute);
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, perMinute / 3),
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1.0 / perMinute),
            QueueLimit = queue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public bool IsBackingOff => _time.GetUtcNow() < _unavailableUntil;

    /// <summary>Waits briefly for a product-read slot (barcode lookups are user-facing).</summary>
    public async Task<bool> TryAcquireProductAsync(CancellationToken ct)
    {
        if (IsBackingOff) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            using var lease = await _product.AcquireAsync(1, timeout.Token);
            return lease.IsAcquired;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Background refreshes never wait; they just try later.</summary>
    public bool TryAcquireProductNow()
    {
        if (IsBackingOff) return false;
        using var lease = _product.AttemptAcquire();
        return lease.IsAcquired;
    }

    public bool TryAcquireSearch()
    {
        if (IsBackingOff) return false;
        using var lease = _search.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void ReportSuccess()
    {
        lock (_lock) _consecutiveFailures = 0;
    }

    public void ReportFailure(TimeSpan? retryAfter = null)
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            var backoff = retryAfter ?? TimeSpan.FromTicks(Math.Min(
                MaxBackoff.Ticks, MinBackoff.Ticks * (1L << Math.Min(_consecutiveFailures - 1, 5))));
            _unavailableUntil = _time.GetUtcNow() + backoff;
        }
    }

    public void Dispose()
    {
        _product.Dispose();
        _search.Dispose();
    }
}
