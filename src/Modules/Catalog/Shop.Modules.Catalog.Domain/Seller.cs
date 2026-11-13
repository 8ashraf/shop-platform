namespace Shop.Modules.Catalog.Domain;

/// <summary>متجر معتمد عند الكتالوج. المفتاح هو رقم المتجر نفسه (OrganizationId).</summary>
public sealed class Seller
{
    private Seller() { }

    public Guid OrganizationId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime RegisteredAt { get; private set; }

    public static Seller Create(Guid organizationId, Guid ownerUserId, DateTime nowUtc) =>
        new()
        {
            OrganizationId = organizationId,
            OwnerUserId = ownerUserId,
            IsActive = true,
            RegisteredAt = nowUtc
        };

    public void Deactivate() => IsActive = false;
}