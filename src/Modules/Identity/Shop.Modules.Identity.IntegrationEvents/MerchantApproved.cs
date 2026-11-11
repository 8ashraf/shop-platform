using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.IntegrationEvents;

/// <summary>بيتبعت لما المنصة توافق على متجر. أي Module تاني يقدر يسمعه.</summary>
public sealed record MerchantApproved(Guid OrganizationId, Guid OwnerUserId) : IntegrationEvent;