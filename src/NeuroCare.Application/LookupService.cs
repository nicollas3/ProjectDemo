using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ILookupService
{
    Task<IReadOnlyList<LookupItemDto>> DoctorsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupItemDto>> ActivePatientsAsync(CancellationToken ct = default);
}

public class LookupService(IDoctorRepository doctors, IPatientRepository patients, AccessGuard guard) : ILookupService
{
    public async Task<IReadOnlyList<LookupItemDto>> DoctorsAsync(CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        return (await doctors.ListActiveAsync(ct)).Select(d => new LookupItemDto(d.Id, d.FullName)).ToList();
    }

    public async Task<IReadOnlyList<LookupItemDto>> ActivePatientsAsync(CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        return (await patients.SearchAsync(null, false, ct)).Select(p => new LookupItemDto(p.Id, p.DisplayName)).ToList();
    }
}
