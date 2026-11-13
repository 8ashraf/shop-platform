using Shop.Modules.Catalog.Application;
using Shop.Modules.Catalog.Domain;
using Shop.Modules.Identity.IntegrationEvents;
using Shop.Shared.Application;

namespace Shop.Modules.Catalog.UnitTests;

public class AccountActivatedHandlerTests
{
    private readonly FakeCatalogStore _store = new();
    private readonly AccountActivatedHandler _handler;

    public AccountActivatedHandlerTests()
    {
        _handler = new AccountActivatedHandler(_store, new FakeClock());
    }

    [Fact]
    public async Task MerchantWithStore_IsRegisteredAsSeller()
    {
        var organizationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await _handler.HandleAsync(new AccountActivated(Guid.NewGuid(), ownerId, "Merchant", organizationId));

        var seller = Assert.Single(_store.Sellers);
        Assert.Equal(organizationId, seller.OrganizationId);
        Assert.Equal(ownerId, seller.OwnerUserId);
        Assert.True(seller.IsActive);
    }

    [Fact]
    public async Task CustomerAccount_IsIgnored()
    {
        await _handler.HandleAsync(new AccountActivated(Guid.NewGuid(), Guid.NewGuid(), "Customer", null));

        Assert.Empty(_store.Sellers);
    }

    [Fact]
    public async Task MerchantWithoutStore_IsIgnored()
    {
        await _handler.HandleAsync(new AccountActivated(Guid.NewGuid(), Guid.NewGuid(), "Merchant", null));

        Assert.Empty(_store.Sellers);
    }

    [Fact]
    public async Task SameEventTwice_RegistersTheSellerOnce()
    {
        var activated = new AccountActivated(Guid.NewGuid(), Guid.NewGuid(), "Merchant", Guid.NewGuid());

        await _handler.HandleAsync(activated);
        await _handler.HandleAsync(activated);

        Assert.Single(_store.Sellers);
    }
}

public class FakeCatalogStore : ICatalogStore
{
    public List<Seller> Sellers { get; } = [];

    public Task<Seller?> FindSellerAsync(Guid organizationId) =>
        Task.FromResult(Sellers.FirstOrDefault(s => s.OrganizationId == organizationId));

    public void AddSeller(Seller seller) => Sellers.Add(seller);

    public Task SaveChangesAsync() => Task.CompletedTask;
}

public class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; } = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
}