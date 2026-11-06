using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Infrastructure.Database.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id).ValueGeneratedNever();

        builder.Property(organization => organization.Name).HasMaxLength(200).IsRequired();

        builder.Ignore(organization => organization.DomainEvents);
    }
}

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");

        // المفتاح من عمودين: نفس الشخص مايتكررش في نفس المتجر
        builder.HasKey(member => new { member.OrgId, member.UserId });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(member => member.OrgId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}