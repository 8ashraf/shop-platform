using Shop.Shared.Kernel;

namespace Shop.Shared.Application;

/// <summary>Publishes integration events to other modules.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}

/// <summary>Handles an integration event published by another (or the same) module.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
