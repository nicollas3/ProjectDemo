using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IReportService
{
    Task<PatientReportDto> GetPatientSummaryAsync(Guid patientId, int days, CancellationToken ct = default);
}

/// <summary>Resumo do prontuário para impressão/PDF. Exclusivo de médicos. Apenas consolida dados registrados (sem interpretação).</summary>
public class ReportService(
    ClinicalContext context, IClinicalNoteRepository notes, IMedicationRepository medications,
    ISymptomRepository symptoms, ISeizureRepository seizures, IFallRepository falls,
    IQuestionnaireResponseRepository questionnaires, IDocumentRepository documents,
    IOrganizationRepository organizations, AccessGuard guard, IAuditService audit, IClock clock) : IReportService
{
    public async Task<PatientReportDto> GetPatientSummaryAsync(Guid patientId, int days, CancellationToken ct = default)
    {
        var org = guard.RequireDoctor();
        var patient = await context.ResolveAsync(patientId, ct);
        var period = Math.Clamp(days, 7, 3650);
        var nowUtc = clock.UtcNow;
        var since = nowUtc.AddDays(-period);

        var meds = (await medications.ListByPatientAsync(patient.Id, ct))
            .Where(m => m.Status != MedicationStatus.Finished)
            .Select(m => new ReportMedicationDto(m.Name, m.Dosage, m.Frequency, m.Status, m.StartDate)).ToList();

        var signedNotes = (await notes.ListByPatientAsync(patient.Id, ct))
            .Where(n => n.Status == NoteStatus.Signed).Take(5)
            .Select(n => new ReportNoteDto(clock.ToLocal(n.OccurredAtUtc), n.Doctor?.FullName ?? "",
                n.Subjective, n.Objective, n.Assessment, n.Plan)).ToList();

        var symptomRows = (await symptoms.ListByPatientAsync(patient.Id, since, ct))
            .GroupBy(s => s.Type)
            .Select(g => new ReportSymptomDto(g.Key, g.Count(), Math.Round(g.Average(x => x.Intensity), 1), g.Max(x => x.Intensity)))
            .OrderByDescending(r => r.Count).ToList();

        var seizureRows = (await seizures.ListByPatientAsync(patient.Id, since, ct))
            .Select(z => new ReportSeizureDto(clock.ToLocal(z.OccurredAtUtc), z.Type, z.DurationSeconds, z.LossOfConsciousness)).ToList();

        var fallRows = (await falls.ListByPatientAsync(patient.Id, since, ct))
            .Select(f => new ReportFallDto(clock.ToLocal(f.OccurredAtUtc), f.Circumstance, f.Injury, f.NeededMedicalCare)).ToList();

        var scales = (await questionnaires.ListByPatientAsync(patient.Id, null, nowUtc.AddYears(-10), ct))
            .GroupBy(r => r.QuestionnaireKey)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(r => r.AnsweredAtUtc).ToList();
                var def = QuestionnaireCatalog.Find(g.Key);
                return new ReportQuestionnaireDto(def?.Title ?? g.Key, clock.ToLocal(ordered[0].AnsweredAtUtc),
                    ordered[0].TotalScore, def?.MaxScore ?? ordered[0].TotalScore, ordered[0].BandLabel,
                    ordered.Count > 1 ? ordered[1].TotalScore : null);
            }).OrderBy(r => r.Title).ToList();

        var docs = (await documents.ListByPatientAsync(patient.Id, ct))
            .Select(d => new ReportDocumentDto(d.Title, d.Category, clock.ToLocal(d.CreatedAt))).ToList();

        var organization = await organizations.GetByIdAsync(org, ct);
        var author = await context.TryCurrentDoctorAsync(ct);
        await audit.RecordAsync(new AuditEntry("Report.Generate", nameof(Patient), patient.Id.ToString()), ct);

        return new PatientReportDto(
            patient.FullName, patient.AgeOn(clock.ToLocal(nowUtc).Date), patient.Sex, patient.MaskedCpf,
            patient.ResponsibleDoctor?.FullName, organization?.Name ?? "", author?.FullName ?? "Médico",
            clock.ToLocal(nowUtc), period, meds, signedNotes, symptomRows, seizureRows, fallRows, scales, docs);
    }
}
