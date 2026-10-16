using Microsoft.AspNetCore.Mvc.Rendering;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Web.Helpers;

public static class EnumText
{
    public static string Text(this Sex v) => v switch
    {
        Sex.Female => "Feminino", Sex.Male => "Masculino", Sex.Other => "Outro", _ => "Não informado"
    };

    public static string Text(this PatientStatus v) => v == PatientStatus.Active ? "Ativo" : "Inativo";

    public static string Text(this AppointmentStatus v) => v switch
    {
        AppointmentStatus.Scheduled => "Agendada", AppointmentStatus.Confirmed => "Confirmada",
        AppointmentStatus.Completed => "Realizada", AppointmentStatus.Cancelled => "Cancelada",
        AppointmentStatus.NoShow => "Faltou", _ => v.ToString()
    };

    public static string Badge(this AppointmentStatus v) => v switch
    {
        AppointmentStatus.Scheduled => "text-bg-secondary", AppointmentStatus.Confirmed => "text-bg-primary",
        AppointmentStatus.Completed => "text-bg-success", AppointmentStatus.Cancelled => "text-bg-dark",
        AppointmentStatus.NoShow => "text-bg-warning", _ => "text-bg-light"
    };

    public static string Text(this AppointmentType v) => v switch
    {
        AppointmentType.FirstVisit => "Primeira consulta", AppointmentType.FollowUp => "Acompanhamento",
        AppointmentType.Return => "Retorno", AppointmentType.Telemedicine => "Telemedicina",
        AppointmentType.Exam => "Exame", _ => v.ToString()
    };

    public static string RoleText(string role) => role switch
    {
        "Doctor" => "Médico(a)", "ClinicAdmin" => "Administrador da clínica", "Patient" => "Paciente",
        "Administrator" => "Administrador da plataforma", "Researcher" => "Pesquisador(a)", _ => role
    };

    public static IEnumerable<SelectListItem> StaffRoleItems() => new[]
    {
        new SelectListItem(RoleText("Doctor"), "Doctor"),
        new SelectListItem(RoleText("ClinicAdmin"), "ClinicAdmin")
    };

    public static IEnumerable<SelectListItem> SexItems() =>
        Enum.GetValues<Sex>().Select(v => new SelectListItem(v.Text(), v.ToString()));

    public static IEnumerable<SelectListItem> SymptomTypeItems() =>
        Enum.GetValues<SymptomType>().Select(v => new SelectListItem(v.Label(), v.ToString()));

    public static IEnumerable<SelectListItem> DocumentCategoryItems() =>
        Enum.GetValues<DocumentCategory>().Select(v => new SelectListItem(v.Label(), v.ToString()));

    public static IEnumerable<SelectListItem> FallCircumstanceItems() =>
        Enum.GetValues<FallCircumstance>().Select(v => new SelectListItem(v.Label(), v.ToString()));

    public static IEnumerable<SelectListItem> SeizureTypeItems() =>
        Enum.GetValues<SeizureType>().Select(v => new SelectListItem(v.Label(), v.ToString()));

    public static string MedicationBadge(this MedicationStatus v) => v switch
    {
        MedicationStatus.Active => "text-bg-success", MedicationStatus.Suspended => "text-bg-warning", _ => "text-bg-secondary"
    };

    public static IEnumerable<SelectListItem> TypeItems() =>
        Enum.GetValues<AppointmentType>().Select(v => new SelectListItem(v.Text(), v.ToString()));
}

public static class ModelStateExtensions
{
    public static void AddErrors(this Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state,
        NeuroCare.Application.RequestValidationException ex)
    {
        foreach (var (field, messages) in ex.Errors)
            foreach (var m in messages) state.AddModelError(field, m);
    }
}
