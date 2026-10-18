using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.OpenFoodFacts;

/// <summary>Barcodes whose cached OFF data is stale and should be refreshed in the background.</summary>
public sealed class OffRefreshQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly ConcurrentDictionary<string, byte> _pending = new();

    public void Enqueue(string code)
    {
        if (_pending.TryAdd(code, 0) && !_channel.Writer.TryWrite(code))
            _pending.TryRemove(code, out _);
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public void Done(string code) => _pending.TryRemove(code, out _);
}

public sealed class OffRefreshWorker(OffRefreshQueue queue, IServiceScopeFactory scopes, ILogger<OffRefreshWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var code in queue.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<OffFoodService>();
                var outcome = await service.RefreshAsync(code, ct);
                if (outcome == OffStatus.Unavailable)
                {
                    // Rate limited or OFF down: try again later without blocking user requests.
                    await Task.Delay(TimeSpan.FromSeconds(20), ct);
                    queue.Done(code);
                    queue.Enqueue(code);
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Background refresh of {Code} failed", code);
            }
            queue.Done(code);
        }
    }
}
