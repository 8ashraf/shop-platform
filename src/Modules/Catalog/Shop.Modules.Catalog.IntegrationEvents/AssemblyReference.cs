using System.Reflection;

namespace Shop.Modules.Catalog.IntegrationEvents;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

// Example:
// public sealed record CatalogItemCreatedIntegrationEvent(Guid ItemId) : Shop.Shared.Kernel.IntegrationEvent;
