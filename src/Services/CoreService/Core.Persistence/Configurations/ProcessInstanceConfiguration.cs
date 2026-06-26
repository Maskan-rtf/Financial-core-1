using Core.Domain.Entities.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Persistence.Configurations;

public sealed class ProcessInstanceConfiguration : IEntityTypeConfiguration<ProcessInstance>
{
    public void Configure(EntityTypeBuilder<ProcessInstance> builder)
    {
        builder.ToTable("process_instances", DbSchemas.Process);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Module).HasConversion<int>().IsRequired();
        builder.Property(x => x.CaseId).IsRequired();
        builder.Property(x => x.WorkflowInstanceId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.WorkflowDefinitionId).HasMaxLength(128);
        builder.Property(x => x.WorkflowDefinitionVersion);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CurrentProcessStep).HasMaxLength(128);
        builder.Property(x => x.LastCommandCorrelationId);
        builder.Property(x => x.StartedAt).HasDefaultValueSql("timezone('utc', now())");
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => new { x.Module, x.CaseId }).IsUnique();
        builder.HasIndex(x => x.WorkflowInstanceId);
        builder.HasIndex(x => new { x.Module, x.Status });
        builder.HasIndex(x => x.LastCommandCorrelationId);
    }
}
