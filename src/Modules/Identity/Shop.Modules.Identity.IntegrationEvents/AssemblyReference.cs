using System.Reflection;

namespace Shop.Modules.Identity.IntegrationEvents;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

// Example:
// public sealed record IdentityItemCreatedIntegrationEvent(Guid ItemId) : Shop.Shared.Kernel.IntegrationEvent;
