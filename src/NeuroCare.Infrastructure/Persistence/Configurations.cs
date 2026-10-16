using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(200);
        b.Property(x => x.DocumentNumber).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(300);
        b.HasIndex(x => x.CreatedAt);
    }
}

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> b)
    {
        b.ToTable("Doctors");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.LicenseNumber).HasMaxLength(30);
        b.Property(x => x.Specialty).HasMaxLength(100);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("Patients");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.SocialName).HasMaxLength(200);
        b.Property(x => x.Cpf).HasMaxLength(11).IsFixedLength().IsRequired();
        b.Property(x => x.BirthDate).HasColumnType("date");
        b.Property(x => x.Sex).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.EmergencyContactName).HasMaxLength(200);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Ignore(x => x.DisplayName);
        b.Ignore(x => x.MaskedCpf);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Cpf }).IsUnique();
        b.HasIndex(x => x.ResponsibleDoctorId);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResponsibleDoctor).WithMany().HasForeignKey(x => x.ResponsibleDoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments", t =>
        {
            t.HasCheckConstraint("CK_Appointments_Duration", "[DurationMinutes] BETWEEN 5 AND 240");
        });
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Ignore(x => x.EndsAtUtc);
        b.Ignore(x => x.IsOpen);

        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.PatientId);
        b.HasIndex(x => new { x.DoctorId, x.StartsAtUtc });
        b.HasIndex(x => new { x.OrganizationId, x.StartsAtUtc });
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Details).HasMaxLength(500);
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        b.HasIndex(x => x.UserId);
    }
}
