namespace Shop.Shared.Kernel;

public static class Permissions
{
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";
    public const string OrdersRead = "orders.read";
    public const string OrdersFulfill = "orders.fulfill";
    public const string FinanceRead = "finance.read";
    public const string StaffManage = "staff.manage";

    public static readonly IReadOnlyList<string> All =
    [
        CatalogRead, CatalogWrite, OrdersRead, OrdersFulfill, FinanceRead, StaffManage
    ];
}