using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IClinicalContext
{
    Task<PatientHeaderDto> GetHeaderAsync(Guid? patientId, CancellationToken ct = default);
}

/// <summary>
/// Resolve o paciente-alvo de uma operação clínica. Se o usuário é paciente, o alvo é SEMPRE ele mesmo
/// (o patientId recebido é ignorado — impede um paciente de acessar outro pela URL).
/// </summary>
public class ClinicalContext(
    IPatientRepository patients, IDoctorRepository doctors, AccessGuard guard, ICurrentUser user, IClock clock) : IClinicalContext
{
    public async Task<Patient> ResolveAsync(Guid? patientId, CancellationToken ct = default)
    {
        guard.RequireOrganization();
        Patient? patient;
        if (guard.IsPatient)
            patient = user.UserId is null ? null : await patients.GetByUserIdAsync(user.UserId.Value, ct);
        else
            patient = patientId is null ? null : await patients.GetByIdAsync(patientId.Value, ct);

        if (patient is null) throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessClinicalData(patient);
        return patient;
    }

    public async Task<PatientHeaderDto> GetHeaderAsync(Guid? patientId, CancellationToken ct = default)
    {
        var p = await ResolveAsync(patientId, ct);
        return new PatientHeaderDto(p.Id, p.DisplayName, p.AgeOn(clock.ToLocal(clock.UtcNow).Date), p.Status);
    }

    public async Task<Doctor?> TryCurrentDoctorAsync(CancellationToken ct = default)
    {
        if (user.UserId is null) return null;
        return await doctors.GetByUserIdAsync(user.UserId.Value, ct);
    }

    public async Task<Doctor> CurrentDoctorAsync(CancellationToken ct = default) =>
        await TryCurrentDoctorAsync(ct) ?? throw new ForbiddenException("Usuário sem registro de médico.");

    public DateTime ParseOccurredAt(DateTime local, string field)
    {
        if (local.Year < 1900) throw new RequestValidationException(field, "Data e horário inválidos.");
        var utc = clock.ToUtc(local);
        if (utc > clock.UtcNow.AddMinutes(5))
            throw new RequestValidationException(field, "Data e horário não podem estar no futuro.");
        return utc;
    }
}
