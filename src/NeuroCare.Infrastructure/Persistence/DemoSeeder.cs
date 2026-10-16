using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

/// <summary>Dados 100% FICTÍCIOS para demonstração. Nunca habilitar em produção.</summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var db = sp.GetRequiredService<NeuroCareDbContext>();
        if (await db.Organizations.AnyAsync(ct)) return;

        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword não configurado; seed de demonstração ignorado.");
            return;
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        async Task<ApplicationUser> CreateUser(string email, string name, Guid? orgId, string role)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                FullName = name, OrganizationId = orgId, Active = true, CreatedAt = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Falha ao criar usuário de demonstração: " +
                                                    string.Join("; ", result.Errors.Select(e => e.Code)));
            await users.AddToRoleAsync(user, role);
            return user;
        }

        var orgA = new Organization { Name = "Clínica Demo A", LegalName = "Clínica Demo A (fictícia)", Email = "contato.a@neurocare.local", Active = true };
        var orgB = new Organization { Name = "Clínica Demo B", LegalName = "Clínica Demo B (fictícia)", Email = "contato.b@neurocare.local", Active = true };
        db.Organizations.AddRange(orgA, orgB);
        await db.SaveChangesAsync(ct);

        await CreateUser("admin@neurocare.local", "Administrador da Plataforma", null, Roles.Administrator);
        var uClinicA = await CreateUser("clinica@neurocare.local", "Gestora Clínica Demo A", orgA.Id, Roles.ClinicAdmin);
        var uDocA = await CreateUser("medico@neurocare.local", "Ana Souza", orgA.Id, Roles.Doctor);
        var uDocB = await CreateUser("medico.b@neurocare.local", "Carlos Lima", orgB.Id, Roles.Doctor);
        var uPatA = await CreateUser("paciente@neurocare.local", "Maria Oliveira", orgA.Id, Roles.Patient);
        _ = uClinicA;

        var docA = new Doctor { OrganizationId = orgA.Id, UserId = uDocA.Id, FullName = "Ana Souza", LicenseNumber = "CRM-XX 00001", Specialty = "Neurologia" };
        var docB = new Doctor { OrganizationId = orgB.Id, UserId = uDocB.Id, FullName = "Carlos Lima", LicenseNumber = "CRM-XX 00002", Specialty = "Neurologia" };
        db.Doctors.AddRange(docA, docB);

        var maria = new Patient { OrganizationId = orgA.Id, UserId = uPatA.Id, ResponsibleDoctorId = docA.Id, FullName = "Maria Oliveira", Cpf = "12345678909", BirthDate = new DateTime(1954, 3, 12), Sex = Sex.Female, Phone = "(79) 90000-0001", Email = "paciente@neurocare.local", Notes = "DEMO (fictício): acompanhamento de Parkinson." };
        var joao = new Patient { OrganizationId = orgA.Id, ResponsibleDoctorId = docA.Id, FullName = "João Pereira", Cpf = "11144477735", BirthDate = new DateTime(1988, 7, 21), Sex = Sex.Male, Phone = "(79) 90000-0002", Notes = "DEMO (fictício): acompanhamento de epilepsia." };
        var antonia = new Patient { OrganizationId = orgA.Id, ResponsibleDoctorId = docA.Id, FullName = "Antônia Ribeiro", Cpf = "52998224725", BirthDate = new DateTime(1961, 11, 2), Sex = Sex.Female, Phone = "(79) 90000-0003", Notes = "DEMO (fictício): acompanhamento pós-AVC." };
        var paulo = new Patient { OrganizationId = orgB.Id, ResponsibleDoctorId = docB.Id, FullName = "Paulo Fernandes", Cpf = "39053344705", BirthDate = new DateTime(1975, 5, 30), Sex = Sex.Male, Notes = "DEMO (fictício): paciente da Organização B." };
        db.Patients.AddRange(maria, joao, antonia, paulo);

        var today = DateTime.UtcNow.Date;
        Appointment Appt(Organization o, Doctor d, Patient p, DateTime startUtc, AppointmentStatus s, AppointmentType t = AppointmentType.FollowUp) =>
            new() { OrganizationId = o.Id, DoctorId = d.Id, PatientId = p.Id, StartsAtUtc = startUtc, Status = s, Type = t, DurationMinutes = 30 };

        db.Appointments.AddRange(
            Appt(orgA, docA, maria, today.AddHours(12), AppointmentStatus.Confirmed),
            Appt(orgA, docA, joao, today.AddHours(14), AppointmentStatus.Scheduled, AppointmentType.Return),
            Appt(orgA, docA, antonia, today.AddDays(1).AddHours(13), AppointmentStatus.Scheduled),
            Appt(orgA, docA, maria, today.AddDays(-1).AddHours(12), AppointmentStatus.Completed),
            Appt(orgA, docA, joao, today.AddDays(-10).AddHours(13), AppointmentStatus.NoShow),
            Appt(orgA, docA, antonia, today.AddDays(-3).AddHours(15), AppointmentStatus.Cancelled),
            Appt(orgB, docB, paulo, today.AddHours(13), AppointmentStatus.Scheduled, AppointmentType.FirstVisit));

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Dados de DEMONSTRAÇÃO criados. Troque/remova as credenciais antes de qualquer uso real.");
    }
}
