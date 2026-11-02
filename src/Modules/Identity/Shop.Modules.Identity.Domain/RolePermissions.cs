using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

public static class RolePermissions
{
    public static IReadOnlyList<string> ForRole(OrgRole role) => role switch
    {
        OrgRole.Owner => Permissions.All,
        OrgRole.Admin =>
        [
            Permissions.CatalogRead, Permissions.CatalogWrite,
            Permissions.OrdersRead, Permissions.OrdersFulfill, Permissions.StaffManage
        ],
        OrgRole.CatalogManager => [Permissions.CatalogRead, Permissions.CatalogWrite],
        OrgRole.OrderFulfiller => [Permissions.OrdersRead, Permissions.OrdersFulfill],
        OrgRole.Finance => [Permissions.FinanceRead, Permissions.OrdersRead],
        _ => []
    };
}