using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations;

public sealed class LoanCaseCommentConfiguration : IEntityTypeConfiguration<LoanCaseComment>
{
    public void Configure(EntityTypeBuilder<LoanCaseComment> builder)
    {
        builder.ToTable("loan_case_comments", DbSchemas.Loan);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CaseId);

        builder.Property(x => x.SenderUserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SenderRole).HasMaxLength(64);
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Phase).HasConversion<int>().IsRequired();
        builder.Property(x => x.WorkflowStatusAtCreation).HasConversion<int?>();

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
        builder.Property(x => x.UpdatedAt);
    }
}
