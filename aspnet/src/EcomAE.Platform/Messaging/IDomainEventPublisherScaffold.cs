namespace EcomAE.Platform.Messaging;

/// <summary>
/// Unwired domain-event publisher contract (Kafka primary / RabbitMQ alternative).
/// Not registered in DI; must not publish until worker dry-run parity is approved.
/// </summary>
public interface IDomainEventPublisherScaffold
{
    Task PublishAsync(string topic, string payloadJson, CancellationToken cancellationToken = default);
}
