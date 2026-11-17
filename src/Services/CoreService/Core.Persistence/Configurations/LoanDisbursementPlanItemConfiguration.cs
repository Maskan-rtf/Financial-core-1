using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations;

public sealed class LoanDisbursementPlanItemConfiguration : IEntityTypeConfiguration<LoanDisbursementPlanItem>
{
    public void Configure(EntityTypeBuilder<LoanDisbursementPlanItem> builder)
    {
        builder.ToTable("loan_disbursement_plan_items", DbSchemas.Loan);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CaseId, x.StageNumber }).IsUnique();

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
        builder.Property(x => x.UpdatedAt);
    }
}
