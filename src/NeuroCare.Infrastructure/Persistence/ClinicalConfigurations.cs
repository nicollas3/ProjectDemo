using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    public void Configure(EntityTypeBuilder<ClinicalNote> b)
    {
        b.ToTable("ClinicalNotes");
        b.Property(x => x.Subjective).HasMaxLength(4000);
        b.Property(x => x.Objective).HasMaxLength(4000);
        b.Property(x => x.Assessment).HasMaxLength(4000);
        b.Property(x => x.Plan).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Ignore(x => x.HasContent);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.OccurredAtUtc });
        b.HasIndex(x => x.DoctorId);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Addenda).WithOne().HasForeignKey(a => a.ClinicalNoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClinicalNoteAddendumConfiguration : IEntityTypeConfiguration<ClinicalNoteAddendum>
{
    public void Configure(EntityTypeBuilder<ClinicalNoteAddendum> b)
    {
        b.ToTable("ClinicalNoteAddenda");
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.ClinicalNoteId);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> b)
    {
        b.ToTable("Medications");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Dosage).HasMaxLength(100).IsRequired();
        b.Property(x => x.Frequency).HasMaxLength(200).IsRequired();
        b.Property(x => x.Instructions).HasMaxLength(1000);
        b.Property(x => x.StartDate).HasColumnType("date");
        b.Property(x => x.EndDate).HasColumnType("date");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ReminderTimes).HasMaxLength(200);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.Status });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PrescribedBy).WithMany().HasForeignKey(x => x.PrescribedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SymptomRecordConfiguration : IEntityTypeConfiguration<SymptomRecord>
{
    public void Configure(EntityTypeBuilder<SymptomRecord> b)
    {
        b.ToTable("SymptomRecords", t => t.HasCheckConstraint("CK_SymptomRecords_Intensity", "[Intensity] BETWEEN 0 AND 10"));
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.OccurredAtUtc });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SeizureEventConfiguration : IEntityTypeConfiguration<SeizureEvent>
{
    public void Configure(EntityTypeBuilder<SeizureEvent> b)
    {
        b.ToTable("SeizureEvents", t => t.HasCheckConstraint("CK_SeizureEvents_Duration", "[DurationSeconds] BETWEEN 1 AND 86400"));
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Trigger).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.OccurredAtUtc });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FallEventConfiguration : IEntityTypeConfiguration<FallEvent>
{
    public void Configure(EntityTypeBuilder<FallEvent> b)
    {
        b.ToTable("FallEvents");
        b.Property(x => x.Circumstance).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.OccurredAtUtc });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuestionnaireResponseConfiguration : IEntityTypeConfiguration<QuestionnaireResponse>
{
    public void Configure(EntityTypeBuilder<QuestionnaireResponse> b)
    {
        b.ToTable("QuestionnaireResponses");
        b.Property(x => x.QuestionnaireKey).HasMaxLength(50).IsRequired();
        b.Property(x => x.AnswersJson).IsRequired();
        b.Property(x => x.BandLabel).HasMaxLength(100);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.AnsweredAtUtc });
        b.HasIndex(x => new { x.OrganizationId, x.SafetyFlag, x.AnsweredAtUtc });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PatientDocumentConfiguration : IEntityTypeConfiguration<PatientDocument>
{
    public void Configure(EntityTypeBuilder<PatientDocument> b)
    {
        b.ToTable("PatientDocuments");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OriginalFileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(100).IsRequired();

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.PatientId, x.CreatedAt });
        b.HasIndex(x => x.StorageKey).IsUnique();

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
