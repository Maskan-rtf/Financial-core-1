using Core.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User", DbSchemas.Identity);
        builder.HasKey(e => e.Id);
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(11);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.NationalCode).HasMaxLength(10);
        builder.Property(e => e.PasswordHash).HasMaxLength(256);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Company).WithMany(x => x.Users).HasForeignKey(x => x.CompanyId);
        builder.HasIndex(e => e.PhoneNumber).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL AND \"IsDeleted\" = false");
        builder.HasIndex(e => e.NationalCode);
    }
}
