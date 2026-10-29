namespace Shop.Shared.Kernel;

/// <summary>
/// Base type for events that cross module boundaries.
/// Concrete events live in each module's IntegrationEvents project.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
