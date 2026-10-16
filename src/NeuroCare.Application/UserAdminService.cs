using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<CreatedUserResult> CreateStaffAsync(CreateStaffUserDto dto, CancellationToken ct = default);
    Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default);
    Task<bool> ResendInviteAsync(Guid userId, CancellationToken ct = default);
    Task<bool> InvitePatientAsync(Guid patientId, CancellationToken ct = default);
}

public class UserAdminService(
    IIdentityGateway identity, IDoctorRepository doctors, IPatientRepository patients, IUnitOfWork uow,
    AccessGuard guard, ICurrentUser user, IAuditService audit, IInviteService invites,
    ILogger<UserAdminService> logger) : IUserAdminService
{
    public async Task<IReadOnlyList<UserSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var org = guard.RequireClinicAdmin();
        return await identity.ListUsersAsync(org, ct);
    }

    public async Task<CreatedUserResult> CreateStaffAsync(CreateStaffUserDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireClinicAdmin();
        DtoValidator.EnsureValid(dto);
        if (dto.Role is not (Roles.Doctor or Roles.ClinicAdmin))
            throw new RequestValidationException(nameof(dto.Role), "Perfil inválido.");

        var email = dto.Email.Trim();
        var name = dto.FullName.Trim();
        var userId = await identity.CreateUserAsync(new NewIdentityUser(email, name, org, dto.Role), ct);

        if (dto.Role == Roles.Doctor)
        {
            await doctors.AddAsync(new Doctor
            {
                OrganizationId = org, UserId = userId, FullName = name,
                LicenseNumber = string.IsNullOrWhiteSpace(dto.LicenseNumber) ? null : dto.LicenseNumber.Trim(),
                Specialty = string.IsNullOrWhiteSpace(dto.Specialty) ? null : dto.Specialty.Trim()
            }, ct);
            await uow.SaveChangesAsync(ct);
        }

        await audit.RecordAsync(new AuditEntry("User.Create", "User", userId.ToString(), $"Role={dto.Role}"), ct);
        logger.LogInformation("Usuário {UserId} criado na organização {OrganizationId}", userId, org);
        var sent = await invites.TrySendAsync(userId, email, name, ct);
        return new CreatedUserResult(userId, sent);
    }

    public async Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var org = guard.RequireClinicAdmin();
        var target = await FindInOrganizationAsync(userId, org, ct);
        if (!active && userId == user.UserId)
            throw new RequestValidationException("", "Você não pode desativar o seu próprio usuário.");

        await identity.SetActiveAsync(userId, active, ct);
        if (target.Role == Roles.Doctor)
        {
            var doctor = await doctors.GetByUserIdAsync(userId, ct);
            if (doctor is not null)
            {
                doctor.Active = active;
                await uow.SaveChangesAsync(ct);
            }
        }
        await audit.RecordAsync(new AuditEntry(active ? "User.Activate" : "User.Deactivate", "User", userId.ToString()), ct);
    }

    public async Task<bool> ResendInviteAsync(Guid userId, CancellationToken ct = default)
    {
        var org = guard.RequireClinicAdmin();
        var target = await FindInOrganizationAsync(userId, org, ct);
        if (!target.Active) throw new RequestValidationException("", "Usuário inativo.");
        if (target.EmailConfirmed)
            throw new RequestValidationException("", "Este usuário já ativou a conta. Use \"Esqueci minha senha\" na tela de login.");

        await audit.RecordAsync(new AuditEntry("User.InviteResent", "User", userId.ToString()), ct);
        return await invites.TrySendAsync(target.Id, target.Email, target.FullName, ct);
    }

    public async Task<bool> InvitePatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var org = guard.RequireClinicalStaff();
        var patient = await patients.GetByIdAsync(patientId, ct) ?? throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessPatient(patient);

        if (patient.UserId.HasValue)
            throw new RequestValidationException("", "Este paciente já possui acesso ao portal.");
        if (string.IsNullOrWhiteSpace(patient.Email))
            throw new RequestValidationException("", "Cadastre um e-mail para o paciente antes de convidá-lo.");

        var email = patient.Email.Trim();
        var userId = await identity.CreateUserAsync(new NewIdentityUser(email, patient.FullName, org, Roles.Patient), ct);
        patient.UserId = userId;
        await uow.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry("Patient.PortalInvite", nameof(Patient), patientId.ToString()), ct);
        return await invites.TrySendAsync(userId, email, patient.FullName, ct);
    }

    private async Task<UserSummaryDto> FindInOrganizationAsync(Guid userId, Guid org, CancellationToken ct)
    {
        var target = await identity.FindAsync(userId, ct);
        // Não revela usuários de outras organizações.
        if (target is null || target.OrganizationId != org) throw new NotFoundException("Usuário não encontrado.");
        return target;
    }
}
