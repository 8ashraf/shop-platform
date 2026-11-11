// <module-usings>
using Shop.Modules.Identity.Infrastructure;
using Shop.Modules.Catalog.Infrastructure;
// </module-usings>
using Shop.Shared.Infrastructure;

namespace Shop.Host;

/// <summary>
/// Every module loaded by the host. Maintained by build/scripts/modmono.sh add-module.
/// Comment a line out to switch a module off.
/// </summary>
public static class ModuleCatalog
{
    public static IModule[] All =>
    [
        // <modules>
        new IdentityModule(),
        new CatalogModule(),
        // </modules>
    ];
}
