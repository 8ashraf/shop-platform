using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.UnitTests.Domain;

public class OrganizationTests
{
    [Fact]
    public void Create_WithBlankName_Fails()
    {
        var result = Organization.Create("   ");

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.Organization.NameRequired, result.Error);
    }

    [Fact]
    public void Create_WithName_StartsPendingAndTrimsName()
    {
        var result = Organization.Create("  Al-Amal  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Al-Amal", result.Value.Name);
        Assert.Equal(OrgStatus.Pending, result.Value.Status);
    }

    [Fact]
    public void Approve_WhenPending_ActivatesAndRaisesEvent()
    {
        var org = Organization.Create("Al-Amal").Value;

        var result = org.Approve();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrgStatus.Active, org.Status);
        var raised = Assert.Single(org.DomainEvents);
        var approved = Assert.IsType<OrganizationApproved>(raised);
        Assert.Equal(org.Id, approved.OrganizationId);
    }

    [Fact]
    public void Approve_Twice_FailsTheSecondTime()
    {
        var org = Organization.Create("Al-Amal").Value;
        org.Approve();

        var result = org.Approve();

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.Organization.NotPending, result.Error);
        Assert.Single(org.DomainEvents); // مفيش حدث تاني
    }

    [Fact]
    public void Reject_WhenPending_SuspendsWithoutRaisingEvent()
    {
        var org = Organization.Create("Al-Amal").Value;

        var result = org.Reject();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrgStatus.Suspended, org.Status);
        Assert.Empty(org.DomainEvents);
    }
}