using Shop.Shared.Application;
using Shop.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Shop.Shared.Infrastructure;

/// <summary>
/// In-process event bus. Swap for an outbox + broker when a module is extracted.
/// </summary>
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
