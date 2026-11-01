using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

// حدث دومين: بيتبعت من الكيان لما المتجر يتوافق عليه
public sealed record OrganizationApproved(Guid OrganizationId) : DomainEvent;

public sealed class Organization : Entity
{
    private Organization() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public OrgStatus Status { get; private set; }

    public static Result<Organization> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Organization>(IdentityErrors.Organization.NameRequired);
        }

        return Result.Success(new Organization
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Status = OrgStatus.Pending
        });
    }

    // الموافقة (KYB): بس لو لسه Pending
    public Result Approve()
    {
        if (Status != OrgStatus.Pending)
        {
            return Result.Failure(IdentityErrors.Organization.NotPending);
        }

        Status = OrgStatus.Active;
        Raise(new OrganizationApproved(Id));
        return Result.Success();
    }

    public Result Reject()
    {
        if (Status != OrgStatus.Pending)
        {
            return Result.Failure(IdentityErrors.Organization.NotPending);
        }

        Status = OrgStatus.Suspended;
        return Result.Success();
    }
}

public sealed class OrganizationMember
{
    private OrganizationMember() { }

    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public OrgRole Role { get; private set; }

    public static OrganizationMember Create(Guid orgId, Guid userId, OrgRole role) =>
        new() { OrgId = orgId, UserId = userId, Role = role };
}