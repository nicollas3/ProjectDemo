using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> b)
    {
        b.ToTable("NotificationDispatches");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OccurrenceKey).HasMaxLength(80).IsRequired();
        b.Property(x => x.ErrorCode).HasMaxLength(100);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Kind, x.ReferenceId, x.RecipientUserId, x.OccurrenceKey }).IsUnique();
    }
}

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b)
    {
        b.ToTable("ConsentRecords");
        b.Property(x => x.PurposeKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.PurposeText).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Version).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.PurposeKey, x.Version, x.GrantedAtUtc });
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClinicQuestionnaireConfiguration : IEntityTypeConfiguration<ClinicQuestionnaire>
{
    public void Configure(EntityTypeBuilder<ClinicQuestionnaire> b)
    {
        b.ToTable("ClinicQuestionnaires");
        b.Property(x => x.Key).HasMaxLength(80).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Prompt).HasMaxLength(500).IsRequired();
        b.Property(x => x.QuestionsJson).IsRequired();
        b.Property(x => x.BandsJson).IsRequired();
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
