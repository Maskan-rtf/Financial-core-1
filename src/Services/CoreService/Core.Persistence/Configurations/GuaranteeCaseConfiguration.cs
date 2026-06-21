using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations;

public sealed class GuaranteeCaseConfiguration : IEntityTypeConfiguration<GuaranteeCase>
{
    public void Configure(EntityTypeBuilder<GuaranteeCase> builder)
    {
        builder.ToTable("guarantee_cases", DbSchemas.Guarantee);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CaseNumber).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.CaseNumber).IsUnique();

        builder.Property(x => x.Title).HasMaxLength(256);

        builder.Property(x => x.ApplicantUserId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ApplicantUserId);

        builder.Property(x => x.ApplicantType).HasConversion<int>().IsRequired();
        builder.Property(x => x.CurrentPhase).HasConversion<int>().IsRequired();
        builder.Property(x => x.CurrentStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => x.CurrentStatus);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.CurrentStatus, x.CreatedAt });
        builder.HasIndex(x => new { x.CurrentPhase, x.CreatedAt });
        builder.HasIndex(x => new { x.ApplicantUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt });

        builder.Property(x => x.WorkflowInstanceId).HasMaxLength(128);

        builder.Property(x => x.CompanyId);
        builder.HasIndex(x => x.CompanyId);

        builder.HasOne(x => x.ApplicantCompany)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.Application)
            .WithOne(x => x.Case)
            .HasForeignKey<GuaranteeCaseApplication>(x => x.CaseId);

        builder.HasOne(x => x.ApprovalForm)
            .WithOne(x => x.Case)
            .HasForeignKey<GuaranteeApprovalForm>(x => x.CaseId);

        builder.HasMany(x => x.Documents).WithOne(x => x.Case).HasForeignKey(x => x.CaseId);
        builder.HasMany(x => x.Comments).WithOne(x => x.Case).HasForeignKey(x => x.CaseId);
        builder.HasMany(x => x.WorkflowHistory).WithOne(x => x.Case).HasForeignKey(x => x.CaseId);
    }
}
