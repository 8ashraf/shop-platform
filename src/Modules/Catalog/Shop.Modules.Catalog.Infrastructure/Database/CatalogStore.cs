using Microsoft.EntityFrameworkCore;
using Shop.Modules.Catalog.Application;
using Shop.Modules.Catalog.Domain;

namespace Shop.Modules.Catalog.Infrastructure.Database;

public class CatalogStore(CatalogDbContext db) : ICatalogStore
{
    public Task<Seller?> FindSellerAsync(Guid organizationId) =>
        db.Sellers.FirstOrDefaultAsync(seller => seller.OrganizationId == organizationId);

    public void AddSeller(Seller seller) => db.Sellers.Add(seller);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
