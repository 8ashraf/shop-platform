using Shop.Modules.Catalog.Domain;
using Shop.Modules.Identity.IntegrationEvents;
using Shop.Shared.Application;

namespace Shop.Modules.Catalog.Application;

public class AccountActivatedHandler(ICatalogStore store, IDateTimeProvider clock)
    : IIntegrationEventHandler<AccountActivated>
{
    public async Task HandleAsync(AccountActivated integrationEvent, CancellationToken cancellationToken = default)
    {
        // بنهتم بحسابات التجار اللي ليها متجر بس
        if (integrationEvent.AccountType != "Merchant" || integrationEvent.OrganizationId is null)
            return;

        var organizationId = integrationEvent.OrganizationId.Value;

        // لو الحدث وصل مرتين، ما نسجّلش المتجر مرتين
        var existing = await store.FindSellerAsync(organizationId);
        if (existing is not null)
            return;

        var seller = Seller.Create(organizationId, integrationEvent.UserId, clock.UtcNow);
        store.AddSeller(seller);
        await store.SaveChangesAsync();
    }
}