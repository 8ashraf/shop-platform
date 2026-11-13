using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Modules.Catalog.Domain;

namespace Shop.Modules.Catalog.Infrastructure.Database.Configurations;

public class SellerConfiguration : IEntityTypeConfiguration<Seller>
{
    public void Configure(EntityTypeBuilder<Seller> builder)
    {
        builder.ToTable("Sellers");

        // المفتاح هو رقم المتجر نفسه (جاي من الـ Identity)، مش بنولّده هنا
        builder.HasKey(seller => seller.OrganizationId);
        builder.Property(seller => seller.OrganizationId).ValueGeneratedNever();
    }
}