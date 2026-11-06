using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Modules.Identity.Domain;

namespace Shop.Modules.Identity.Infrastructure.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Phone).HasMaxLength(20).IsRequired();
        builder.HasIndex(user => user.Phone).IsUnique();

        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.PasswordHash).HasMaxLength(256);

        // أحداث الدومين مش جدول ولا عمود
        builder.Ignore(user => user.DomainEvents);
    }
}