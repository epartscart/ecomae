using System.Collections.Concurrent;
using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.EventBus;

public sealed class InMemoryLifeOsEventBus : ILifeOsEventBus
{
    private readonly ConcurrentQueue<LifeOsEvent> _recent = new();
    private readonly ConcurrentDictionary<Guid, Func<LifeOsEvent, CancellationToken, Task>> _handlers = new();
    private const int MaxRecent = 200;

    public async Task PublishAsync(LifeOsEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _recent.Enqueue(@event);
        while (_recent.Count > MaxRecent && _recent.TryDequeue(out _))
        {
        }

        foreach (var handler in _handlers.Values)
        {
            await handler(@event, cancellationToken).ConfigureAwait(false);
        }
    }

    public IReadOnlyList<LifeOsEvent> Recent(int take = 50)
        => _recent.Reverse().Take(Math.Clamp(take, 1, MaxRecent)).ToList();

    public IDisposable Subscribe(Func<LifeOsEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var id = Guid.NewGuid();
        _handlers[id] = handler;
        return new Subscription(() => _handlers.TryRemove(id, out _));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                dispose();
            }
        }
    }
}
