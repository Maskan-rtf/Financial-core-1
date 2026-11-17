using Core.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations.Identity;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Company", DbSchemas.Identity);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.EconomicCode).IsRequired().HasMaxLength(32);
        builder.Property(e => e.RegistrationNumber).HasMaxLength(64);
        builder.Property(e => e.NationalId).HasMaxLength(64);
        builder.Property(e => e.PhoneNumber).HasMaxLength(32);
        builder.Property(e => e.Address).HasMaxLength(1024);
        builder.Property(e => e.City).HasMaxLength(128);
        builder.Property(e => e.Province).HasMaxLength(128);
        builder.Property(e => e.PostalCode).HasMaxLength(32);
        builder.Property(e => e.RepresentativeName).HasMaxLength(128);
        builder.Property(e => e.RepresentativePhone).HasMaxLength(32);
        builder.Property(e => e.CeoName).HasMaxLength(128);
        builder.Property(e => e.CeoPhone).HasMaxLength(32);
        builder.Property(e => e.RegistrationPlace).HasMaxLength(256);
        builder.Property(e => e.InitialCapitalMillions).HasPrecision(18, 2);
        builder.Property(e => e.CurrentCapitalMillions).HasPrecision(18, 2);
        builder.Property(e => e.ActivitySubject).HasMaxLength(2048);
        builder.Property(e => e.Website).HasMaxLength(512);
        builder.Property(e => e.CompanyEmail).HasMaxLength(256);
        builder.Property(e => e.LogoS3Key).HasMaxLength(512);

        builder.HasQueryFilter(e => !e.IsDeleted && !e.OwnerUser.IsDeleted);

        builder.HasOne(e => e.OwnerUser)
            .WithMany()
            .HasForeignKey(e => e.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.RegistrationNumber);
        builder.HasIndex(e => e.OwnerUserId);
    }
}
