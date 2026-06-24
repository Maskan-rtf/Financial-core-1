using Core.Domain.Entities.Guarantee;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations;

public sealed class GuaranteeAmendmentHistoryRecordConfiguration : IEntityTypeConfiguration<GuaranteeAmendmentHistoryRecord>
{
    public void Configure(EntityTypeBuilder<GuaranteeAmendmentHistoryRecord> builder)
    {
        builder.ToTable("guarantee_amendment_history_records", DbSchemas.Guarantee);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AmendmentType).HasConversion<int>().IsRequired();
        builder.Property(x => x.PreviousValues).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.NewValues).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ApprovalUser).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

        builder.HasIndex(x => new { x.GuaranteeCaseId, x.CreatedAt });
        builder.HasIndex(x => new { x.GuaranteeCaseId, x.Status });

        builder.HasOne(x => x.GuaranteeCase)
            .WithMany(x => x.AmendmentHistoryRecords)
            .HasForeignKey(x => x.GuaranteeCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
