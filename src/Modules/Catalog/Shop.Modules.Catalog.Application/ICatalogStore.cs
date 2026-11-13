using Shop.Modules.Catalog.Domain;

namespace Shop.Modules.Catalog.Application;

/// <summary>كل اللي الكتالوج محتاجه من قاعدة البيانات.</summary>
public interface ICatalogStore
{
    Task<Seller?> FindSellerAsync(Guid organizationId);
    void AddSeller(Seller seller);

    Task SaveChangesAsync();
}