using Microsoft.Extensions.DependencyInjection;

namespace NeuroCare.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessGuard>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ClinicalContext>();
        services.AddScoped<IClinicalContext>(sp => sp.GetRequiredService<ClinicalContext>());
        services.AddScoped<IClinicalNoteService, ClinicalNoteService>();
        services.AddScoped<IMedicationService, MedicationService>();
        services.AddScoped<ISymptomService, SymptomService>();
        services.AddScoped<ISeizureService, SeizureService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IFallService, FallService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IQuestionnaireDefinitionProvider, QuestionnaireDefinitionProvider>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
        services.AddScoped<IClinicQuestionnaireService, ClinicQuestionnaireService>();
        services.AddScoped<ISecurityNotificationService, SecurityNotificationService>();
        services.AddScoped<ILgpdService, LgpdService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        return services;
    }
}
