using Shop.Modules.Identity.Domain;
using Shop.Modules.Identity.IntegrationEvents;
using Shop.Shared.Application;
using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Application;

public record MerchantRegistration(Guid OrganizationId, Guid AccountId);

public static class MerchantErrors
{
    public static readonly Error AlreadyMerchant =
        new("Identity.Merchant.AlreadyMerchant", "This user already has a merchant account.");

    public static readonly Error OrganizationNotFound =
        new("Identity.Merchant.OrganizationNotFound", "Organization was not found.");

    public static readonly Error OwnerNotFound =
        new("Identity.Merchant.OwnerNotFound", "Organization has no owner.");
}

public class MerchantService(IIdentityStore store, IEventBus eventBus)
{
    // ---------------------------------------------------------------
    // 1) تسجيل تاجر: متجر + عضوية Owner + حساب Merchant (الكل Pending)
    // ---------------------------------------------------------------
    public async Task<Result<MerchantRegistration>> RegisterAsync(Guid userId, string storeName)
    {
        // أ) عنده حساب تاجر بالفعل؟
        var existing = await store.FindAccountAsync(userId, AccountType.Merchant);
        if (existing is not null)
            return Result.Failure<MerchantRegistration>(MerchantErrors.AlreadyMerchant);

        // ب) المتجر (الاسم لازم مش فاضي)
        var organization = Organization.Create(storeName);
        if (organization.IsFailure)
            return Result.Failure<MerchantRegistration>(organization.Error);

        // ج) حساب التاجر وعضوية المالك
        var account = Account.CreateMerchant(userId);
        var owner = OrganizationMember.Create(organization.Value.Id, userId, OrgRole.Owner);

        store.AddOrganization(organization.Value);
        store.AddAccount(account);
        store.AddMember(owner);

        // د) حفظ واحد = إما التلاتة أو ولا واحد
        await store.SaveChangesAsync();

        return Result.Success(new MerchantRegistration(organization.Value.Id, account.Id));
    }

    // ---------------------------------------------------------------
    // 2) موافقة المنصة (KYB): المتجر وحساب المالك يبقوا Active
    // ---------------------------------------------------------------
    public async Task<Result> ApproveAsync(Guid organizationId)
    {
        var organization = await store.FindOrganizationByIdAsync(organizationId);
        if (organization is null)
            return Result.Failure(MerchantErrors.OrganizationNotFound);

        var owner = await store.FindOwnerAsync(organizationId);
        if (owner is null)
            return Result.Failure(MerchantErrors.OwnerNotFound);

        var merchantAccount = await store.FindAccountAsync(owner.UserId, AccountType.Merchant);
        if (merchantAccount is null)
            return Result.Failure(MerchantErrors.OwnerNotFound);

        // القواعد في الـ Domain (بيفشل لو المتجر مش Pending)
        var approval = organization.Approve();
        if (approval.IsFailure)
            return approval;

        var activation = merchantAccount.Activate();
        if (activation.IsFailure)
            return activation;

        await store.SaveChangesAsync();

        // نبلّغ باقي الـ Modules بعد ما الحفظ ينجح
        await eventBus.PublishAsync(new MerchantApproved(organization.Id, owner.UserId));

        return Result.Success();
    }
}