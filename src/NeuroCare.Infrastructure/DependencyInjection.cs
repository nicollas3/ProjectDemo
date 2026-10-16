using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection não configurada. Use User Secrets ou a variável de ambiente ConnectionStrings__DefaultConnection.");

        services.AddDbContext<NeuroCareDbContext>(o =>
            o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NeuroCareDbContext>());
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IClinicalNoteRepository, ClinicalNoteRepository>();
        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<ISymptomRepository, SymptomRepository>();
        services.AddScoped<ISeizureRepository, SeizureRepository>();
        services.AddScoped<IFallRepository, FallRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IQuestionnaireResponseRepository, QuestionnaireResponseRepository>();
        services.AddScoped<IClinicQuestionnaireRepository, ClinicQuestionnaireRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<ILgpdExportRepository, LgpdExportRepository>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddSingleton<IClock, BrazilClock>();
        services.AddHostedService<ReminderWorker>();

        // E-mail: SMTP se configurado; log (somente desenvolvimento) se habilitado; caso contrário, falha explícita.
        if (!string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else if (configuration.GetValue<bool>("Email:UseLogSender"))
            services.AddScoped<IEmailSender, LogEmailSender>();
        else
            services.AddScoped<IEmailSender, UnconfiguredEmailSender>();
        return services;
    }
}
