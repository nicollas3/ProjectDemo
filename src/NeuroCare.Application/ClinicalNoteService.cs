using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IClinicalNoteService
{
    Task<IReadOnlyList<ClinicalNoteListItemDto>> ListAsync(Guid patientId, CancellationToken ct = default);
    Task<ClinicalNoteDetailsDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ClinicalNoteEditModel> GetForEditAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid patientId, SaveClinicalNoteDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, SaveClinicalNoteDto dto, CancellationToken ct = default);
    Task SignAsync(Guid id, CancellationToken ct = default);
    Task AddAddendumAsync(Guid id, AddendumDto dto, CancellationToken ct = default);
}

public class ClinicalNoteService(
    ClinicalContext context, IClinicalNoteRepository notes, IUnitOfWork uow, AccessGuard guard,
    IAuditService audit, IClock clock, ILogger<ClinicalNoteService> logger) : IClinicalNoteService
{
    public async Task<IReadOnlyList<ClinicalNoteListItemDto>> ListAsync(Guid patientId, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var patient = await context.ResolveAsync(patientId, ct);
        var list = await notes.ListByPatientAsync(patient.Id, ct);
        await audit.RecordAsync(new AuditEntry("ClinicalNote.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.Select(n => new ClinicalNoteListItemDto(
            n.Id, clock.ToLocal(n.OccurredAtUtc), n.Doctor?.FullName ?? "", n.Status, Preview(n))).ToList();
    }

    public async Task<ClinicalNoteDetailsDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var n = await LoadAsync(id, ct);
        var me = await context.TryCurrentDoctorAsync(ct);
        await audit.RecordAsync(new AuditEntry("ClinicalNote.View", nameof(ClinicalNote), id.ToString()), ct);

        return new ClinicalNoteDetailsDto(
            n.Id, n.PatientId, clock.ToLocal(n.OccurredAtUtc), n.Doctor?.FullName ?? "", n.Status,
            n.SignedAtUtc.HasValue ? clock.ToLocal(n.SignedAtUtc.Value) : null,
            n.Subjective, n.Objective, n.Assessment, n.Plan,
            n.Addenda.OrderBy(a => a.CreatedAt)
                .Select(a => new AddendumViewDto(clock.ToLocal(a.CreatedAt), a.Doctor?.FullName ?? "", a.Text)).ToList(),
            n.Status == NoteStatus.Draft && me is not null && n.DoctorId == me.Id);
    }

    public async Task<ClinicalNoteEditModel> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var doctor = await context.CurrentDoctorAsync(ct);
        var n = await LoadAsync(id, ct);
        if (n.DoctorId != doctor.Id) throw new ForbiddenException("Somente o autor pode editar esta evolução.");
        return new ClinicalNoteEditModel(n.PatientId, n.Status == NoteStatus.Signed, new SaveClinicalNoteDto
        {
            OccurredAtLocal = clock.ToLocal(n.OccurredAtUtc),
            Subjective = n.Subjective, Objective = n.Objective, Assessment = n.Assessment, Plan = n.Plan
        });
    }

    public async Task<Guid> CreateAsync(Guid patientId, SaveClinicalNoteDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireDoctor();
        DtoValidator.EnsureValid(dto);
        var patient = await context.ResolveAsync(patientId, ct);
        var doctor = await context.CurrentDoctorAsync(ct);

        var note = new ClinicalNote { OrganizationId = org, PatientId = patient.Id, DoctorId = doctor.Id, Status = NoteStatus.Draft };
        Apply(dto, note);
        await notes.AddAsync(note, ct);
        await uow.SaveChangesAsync(ct);

        // Auditoria registra somente metadados, nunca o conteúdo clínico.
        await audit.RecordAsync(new AuditEntry("ClinicalNote.Create", nameof(ClinicalNote), note.Id.ToString()), ct);
        return note.Id;
    }

    public async Task UpdateAsync(Guid id, SaveClinicalNoteDto dto, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var doctor = await context.CurrentDoctorAsync(ct);
        var n = await LoadAsync(id, ct);
        if (n.DoctorId != doctor.Id) throw new ForbiddenException("Somente o autor pode editar esta evolução.");
        DtoValidator.EnsureValid(dto);
        try { n.EnsureEditable(); }
        catch (DomainException ex) { throw new RequestValidationException("", ex.Message); }

        Apply(dto, n);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("ClinicalNote.Update", nameof(ClinicalNote), id.ToString()), ct);
    }

    public async Task SignAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var doctor = await context.CurrentDoctorAsync(ct);
        var n = await LoadAsync(id, ct);
        if (n.DoctorId != doctor.Id) throw new ForbiddenException("Somente o autor pode assinar esta evolução.");
        try { n.Sign(clock.UtcNow); }
        catch (DomainException ex) { throw new RequestValidationException("", ex.Message); }

        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("ClinicalNote.Sign", nameof(ClinicalNote), id.ToString()), ct);
        logger.LogInformation("Evolução {NoteId} assinada", id);
    }

    public async Task AddAddendumAsync(Guid id, AddendumDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireDoctor();
        DtoValidator.EnsureValid(dto);
        var doctor = await context.CurrentDoctorAsync(ct);
        var n = await LoadAsync(id, ct);
        if (n.Status != NoteStatus.Signed)
            throw new RequestValidationException("", "Adendos só podem ser feitos em evoluções assinadas; edite o rascunho.");

        await notes.AddAddendumAsync(new ClinicalNoteAddendum
        { OrganizationId = org, ClinicalNoteId = id, DoctorId = doctor.Id, Text = dto.Text.Trim() }, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("ClinicalNote.Addendum", nameof(ClinicalNote), id.ToString()), ct);
    }

    private async Task<ClinicalNote> LoadAsync(Guid id, CancellationToken ct)
    {
        var org = guard.RequireOrganization();
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null || n.OrganizationId != org) throw new NotFoundException("Evolução não encontrada.");
        return n;
    }

    private void Apply(SaveClinicalNoteDto dto, ClinicalNote n)
    {
        n.OccurredAtUtc = context.ParseOccurredAt(dto.OccurredAtLocal, nameof(dto.OccurredAtLocal));
        n.Subjective = Clean(dto.Subjective);
        n.Objective = Clean(dto.Objective);
        n.Assessment = Clean(dto.Assessment);
        n.Plan = Clean(dto.Plan);
        if (!n.HasContent) throw new RequestValidationException("", "Preencha ao menos uma seção (S, O, A ou P).");
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Preview(ClinicalNote n)
    {
        var text = n.Assessment ?? n.Subjective ?? n.Plan ?? n.Objective ?? "";
        return text.Length <= 140 ? text : text[..140] + "…";
    }
}
