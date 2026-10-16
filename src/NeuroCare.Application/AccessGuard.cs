using System.ComponentModel.DataAnnotations;
using NeuroCare.Domain;

namespace NeuroCare.Application;

/// <summary>
/// Regras centralizadas de autorização por recurso e por organização.
/// Defesa em profundidade: além do filtro global do EF Core, os serviços validam aqui.
/// </summary>
public class AccessGuard(ICurrentUser user)
{
    public bool IsClinicalStaff => user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.ClinicAdmin);
    public bool IsPatient => user.IsInRole(Roles.Patient);
    public bool IsClinicAdmin => user.IsInRole(Roles.ClinicAdmin);
    public bool IsDoctor => user.IsInRole(Roles.Doctor);

    /// <summary>Conteúdo clínico (evolução) é restrito a médicos; ClinicAdmin não lê prontuário.</summary>
    public Guid RequireDoctor()
    {
        var org = RequireOrganization();
        if (!IsDoctor) throw new ForbiddenException();
        return org;
    }

    /// <summary>Dados clínicos: médico da organização ou o próprio paciente.</summary>
    public void EnsureCanAccessClinicalData(Patient patient)
    {
        var org = RequireOrganization();
        if (patient.OrganizationId != org) throw new NotFoundException("Paciente não encontrado.");
        if (IsDoctor) return;
        if (IsPatient && patient.UserId.HasValue && patient.UserId == user.UserId) return;
        throw new ForbiddenException();
    }

    public void RequireAdministrator()
    {
        if (!user.IsAuthenticated || !user.IsInRole(Roles.Administrator)) throw new ForbiddenException();
    }

    public Guid RequireClinicAdmin()
    {
        var org = RequireOrganization();
        if (!IsClinicAdmin) throw new ForbiddenException();
        return org;
    }

    public Guid RequireOrganization()
    {
        if (!user.IsAuthenticated || user.OrganizationId is null) throw new ForbiddenException();
        return user.OrganizationId.Value;
    }

    public Guid RequireClinicalStaff()
    {
        var org = RequireOrganization();
        if (!IsClinicalStaff) throw new ForbiddenException();
        return org;
    }

    public void EnsureCanAccessPatient(Patient patient)
    {
        var org = RequireOrganization();
        // Não revela a existência de registros de outra organização.
        if (patient.OrganizationId != org) throw new NotFoundException("Paciente não encontrado.");
        if (IsClinicalStaff) return;
        if (IsPatient && patient.UserId.HasValue && patient.UserId == user.UserId) return;
        throw new ForbiddenException();
    }
}

public static class DtoValidator
{
    public static void EnsureValid(object dto)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true)) return;

        var errors = results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { string.Empty })
                .Select(m => new { Member = m, Message = r.ErrorMessage ?? "Valor inválido." }))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
        throw new RequestValidationException(errors);
    }
}
