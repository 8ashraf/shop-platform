using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.IntegrationEvents;

/// <summary>
/// بيتبعت لما حساب يتفعّل. أي Module تاني يقدر يسمعه ويفلتر بـ AccountType.
/// OrganizationId فاضي لحسابات الأفراد (Customer).
/// </summary>
public sealed record AccountActivated(
    Guid AccountId,
    Guid UserId,
    string AccountType,
    Guid? OrganizationId) : IntegrationEvent;