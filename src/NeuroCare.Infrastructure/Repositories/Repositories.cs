using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class PatientRepository(NeuroCareDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Patients.Include(p => p.ResponsibleDoctor).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Patient?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Patients.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<IReadOnlyList<Patient>> SearchAsync(string? term, bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<Patient> q = db.Patients.AsNoTracking().Include(p => p.ResponsibleDoctor);
        if (!includeInactive) q = q.Where(p => p.Status == PatientStatus.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            var digits = Cpf.Normalize(t);
            var byCpf = digits.Length == 11;
            q = q.Where(p => p.FullName.Contains(t)
                             || (p.SocialName != null && p.SocialName.Contains(t))
                             || (byCpf && p.Cpf == digits));
        }
        return await q.OrderBy(p => p.FullName).Take(500).ToListAsync(ct);
    }

    public Task<bool> CpfExistsAsync(string cpf, Guid? excludeId, CancellationToken ct = default) =>
        db.Patients.AnyAsync(p => p.Cpf == cpf && (excludeId == null || p.Id != excludeId.Value), ct);

    public async Task AddAsync(Patient patient, CancellationToken ct = default) =>
        await db.Patients.AddAsync(patient, ct);

    public Task<int> CountByStatusAsync(PatientStatus status, CancellationToken ct = default) =>
        db.Patients.CountAsync(p => p.Status == status, ct);

    public async Task<IReadOnlyList<Patient>> RecentAsync(int take, CancellationToken ct = default) =>
        await db.Patients.AsNoTracking().Include(p => p.ResponsibleDoctor)
            .OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync(ct);

    public Task<int> CountWithoutFollowUpAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.Patients.CountAsync(p => p.Status == PatientStatus.Active
            && !db.Appointments.Any(a => a.PatientId == p.Id
                                         && a.StartsAtUtc >= sinceUtc
                                         && a.Status != AppointmentStatus.Cancelled), ct);
}

public class DoctorRepository(NeuroCareDbContext db) : IDoctorRepository
{
    public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Doctors.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Doctor>> ListActiveAsync(CancellationToken ct = default) =>
        await db.Doctors.AsNoTracking().Where(d => d.Active).OrderBy(d => d.FullName).ToListAsync(ct);

    public Task<int> CountActiveAsync(CancellationToken ct = default) =>
        db.Doctors.CountAsync(d => d.Active, ct);

    public Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId, ct);

    public async Task AddAsync(Doctor doctor, CancellationToken ct = default) =>
        await db.Doctors.AddAsync(doctor, ct);
}

public class OrganizationRepository(NeuroCareDbContext db) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking().OrderBy(o => o.Name).ToListAsync(ct);

    public async Task AddAsync(Organization organization, CancellationToken ct = default) =>
        await db.Organizations.AddAsync(organization, ct);

    public Task<bool> IsActiveAsync(Guid id, CancellationToken ct = default) =>
        db.Organizations.AnyAsync(o => o.Id == id && o.Active, ct);
}

public class AppointmentRepository(NeuroCareDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Appointments.Include(a => a.Patient).Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        DateTime? fromUtc, DateTime? toUtc, Guid? doctorId, Guid? patientId, CancellationToken ct = default)
    {
        IQueryable<Appointment> q = db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.Doctor);
        if (fromUtc.HasValue) q = q.Where(a => a.StartsAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(a => a.StartsAtUtc < toUtc.Value);
        if (doctorId.HasValue) q = q.Where(a => a.DoctorId == doctorId.Value);
        if (patientId.HasValue) q = q.Where(a => a.PatientId == patientId.Value);
        return await q.OrderBy(a => a.StartsAtUtc).Take(1000).ToListAsync(ct);
    }

    public Task<bool> HasConflictAsync(Guid doctorId, DateTime startUtc, DateTime endUtc, Guid? excludeId, CancellationToken ct = default) =>
        db.Appointments.AnyAsync(a => a.DoctorId == doctorId
            && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow
            && (excludeId == null || a.Id != excludeId.Value)
            && a.StartsAtUtc < endUtc
            && a.StartsAtUtc.AddMinutes(a.DurationMinutes) > startUtc, ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct = default) =>
        await db.Appointments.AddAsync(appointment, ct);

    public async Task<IReadOnlyDictionary<AppointmentStatus, int>> CountByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.Appointments
            .Where(a => a.StartsAtUtc >= fromUtc && a.StartsAtUtc < toUtc)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

    public Task<int> CountUpcomingAsync(DateTime fromUtc, CancellationToken ct = default) =>
        db.Appointments.CountAsync(a => a.StartsAtUtc >= fromUtc
            && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed), ct);
}

public class AuditLogRepository(NeuroCareDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await db.AuditLogs.AddAsync(log, ct);
}
