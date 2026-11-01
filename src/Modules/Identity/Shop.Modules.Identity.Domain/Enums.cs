namespace Shop.Modules.Identity.Domain;

// الترتيب مهم: الأرقام بتتخزن في الـ database، فالإضافة تبقى في الآخر بس
public enum AccountType { Customer, Merchant, PlatformStaff }
public enum AccountStatus { Pending, Active, Suspended }
public enum OrgStatus { Pending, Active, Suspended }
public enum OrgRole { Owner, Admin, CatalogManager, OrderFulfiller, Finance }