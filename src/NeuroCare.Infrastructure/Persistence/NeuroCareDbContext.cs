using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class NeuroCareDbContext : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>, IUnitOfWork
{
    private readonly ICurrentUser _currentUser;

    public NeuroCareDbContext(DbContextOptions<NeuroCareDbContext> options, ICurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    // Lido pelo filtro global de consulta a cada query (avaliado por instância do contexto).
    private Guid? CurrentOrganizationId => _currentUser.OrganizationId;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<ClinicalNoteAddendum> ClinicalNoteAddenda => Set<ClinicalNoteAddendum>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<SymptomRecord> SymptomRecords => Set<SymptomRecord>();
    public DbSet<SeizureEvent> SeizureEvents => Set<SeizureEvent>();
    public DbSet<FallEvent> FallEvents => Set<FallEvent>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<QuestionnaireResponse> QuestionnaireResponses => Set<QuestionnaireResponse>();
    public DbSet<ClinicQuestionnaire> ClinicQuestionnaires => Set<ClinicQuestionnaire>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<NotificationDispatch> NotificationDispatches => Set<NotificationDispatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NeuroCareDbContext).Assembly);

        // Isolamento multi-tenant centralizado: sem organização no contexto => nenhum dado clínico.
        builder.Entity<Doctor>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<Patient>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<Appointment>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<ClinicalNote>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<ClinicalNoteAddendum>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<Medication>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<SymptomRecord>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<SeizureEvent>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<FallEvent>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<PatientDocument>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<QuestionnaireResponse>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<ClinicQuestionnaire>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<ConsentRecord>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
        builder.Entity<NotificationDispatch>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestampsAndTenantGuard();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestampsAndTenantGuard();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyTimestampsAndTenantGuard()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }

        var org = _currentUser.OrganizationId;
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            // Usuário autenticado só grava na própria organização (o seed roda sem usuário).
            if (_currentUser.IsAuthenticated && (org is null || entry.Entity.OrganizationId != org.Value))
                throw new TenantViolationException();
        }
    }
}
