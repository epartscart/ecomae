using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.EventBus;

/// <summary>Part 2 Ch.7 — central event bus. Scaffold is in-process; Kafka/Rabbit remain unwired.</summary>
public interface ILifeOsEventBus
{
    Task PublishAsync(LifeOsEvent @event, CancellationToken cancellationToken = default);

    IReadOnlyList<LifeOsEvent> Recent(int take = 50);

    IDisposable Subscribe(Func<LifeOsEvent, CancellationToken, Task> handler);
}
