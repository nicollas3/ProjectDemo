using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ITimelineService
{
    Task<IReadOnlyList<TimelineItemDto>> GetAsync(Guid? patientId, CancellationToken ct = default);
}

/// <summary>Linha do tempo unificada. Evoluções (SOAP) só aparecem para médicos; o paciente vê apenas o que é dele.</summary>
public class TimelineService(
    ClinicalContext context, IAppointmentRepository appointments, IClinicalNoteRepository notes,
    IMedicationRepository medications, ISymptomRepository symptoms, ISeizureRepository seizures,
    IFallRepository falls, IQuestionnaireResponseRepository questionnaires, IDocumentRepository documents, AccessGuard guard, IAuditService audit, IClock clock) : ITimelineService
{
    public async Task<IReadOnlyList<TimelineItemDto>> GetAsync(Guid? patientId, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var since = clock.UtcNow.AddYears(-10);
        var items = new List<TimelineItemDto>();

        foreach (var a in await appointments.ListAsync(null, null, null, patient.Id, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(a.StartsAtUtc), TimelineKind.Appointment,
                $"Consulta – {a.Type.Label()}", $"{a.Status.Label()} · {a.Doctor?.FullName}", a.Id));

        if (guard.IsDoctor)
            foreach (var n in await notes.ListByPatientAsync(patient.Id, ct))
                items.Add(new TimelineItemDto(clock.ToLocal(n.OccurredAtUtc), TimelineKind.Note,
                    "Evolução clínica", $"{n.Status.Label()} · {n.Doctor?.FullName}", n.Id));

        foreach (var m in await medications.ListByPatientAsync(patient.Id, ct))
        {
            items.Add(new TimelineItemDto(m.StartDate, TimelineKind.Medication,
                $"Início do medicamento: {m.Name} {m.Dosage}", m.Frequency, m.Id));
            if (m.EndDate.HasValue)
                items.Add(new TimelineItemDto(m.EndDate.Value, TimelineKind.Medication,
                    $"Fim do medicamento: {m.Name}", m.Status.Label(), m.Id));
        }

        foreach (var s in await symptoms.ListByPatientAsync(patient.Id, since, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(s.OccurredAtUtc), TimelineKind.Symptom,
                $"Sintoma: {s.Type.Label()}", $"Intensidade {s.Intensity}/10", s.Id));

        foreach (var z in await seizures.ListByPatientAsync(patient.Id, since, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(z.OccurredAtUtc), TimelineKind.Seizure,
                $"Crise: {z.Type.Label()}", $"{z.DurationSeconds} s", z.Id));

        foreach (var f in await falls.ListByPatientAsync(patient.Id, since, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(f.OccurredAtUtc), TimelineKind.Fall,
                "Queda", f.Injury ? $"{f.Circumstance.Label()} · com lesão" : f.Circumstance.Label(), f.Id));

        foreach (var q in await questionnaires.ListByPatientAsync(patient.Id, null, since, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(q.AnsweredAtUtc), TimelineKind.Questionnaire,
                $"Questionário: {QuestionnaireCatalog.Find(q.QuestionnaireKey)?.Title ?? q.QuestionnaireKey}",
                $"Pontuação {q.TotalScore} – {q.BandLabel}", q.Id));

        foreach (var d in await documents.ListByPatientAsync(patient.Id, ct))
            items.Add(new TimelineItemDto(clock.ToLocal(d.CreatedAt), TimelineKind.Document,
                $"Documento: {d.Title}", d.Category.Label(), d.Id));

        await audit.RecordAsync(new AuditEntry("Timeline.View", nameof(Patient), patient.Id.ToString()), ct);
        return items.OrderByDescending(i => i.WhenLocal).Take(300).ToList();
    }
}
