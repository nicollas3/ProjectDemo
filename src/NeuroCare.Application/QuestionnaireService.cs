using System.Text.Json;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IQuestionnaireService
{
    Task<IReadOnlyList<QuestionnaireSummaryDto>> ListAvailableAsync(CancellationToken ct = default);
    Task<QuestionnaireFormDto> GetFormAsync(string key, CancellationToken ct = default);
    Task<QuestionnaireResultDto> SubmitAsync(Guid? patientId, string key, IReadOnlyDictionary<int, int> answers, CancellationToken ct = default);
    Task<QuestionnaireResultDto> GetResultAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionnaireResponseListItemDto>> ListResponsesAsync(Guid? patientId, string? key, int days, CancellationToken ct = default);
}

/// <summary>Motor genérico de questionários com pontuação por soma. Não interpreta nem diagnostica.</summary>
public class QuestionnaireService(
    ClinicalContext context, IQuestionnaireResponseRepository responses, IUnitOfWork uow,
    AccessGuard guard, ICurrentUser user, IAuditService audit, IClock clock,
    IQuestionnaireDefinitionProvider definitions, ISecurityNotificationService notifications) : IQuestionnaireService
{
    public async Task<IReadOnlyList<QuestionnaireSummaryDto>> ListAvailableAsync(CancellationToken ct = default)
    {
        RequireClinicalActor();
        var all = await definitions.ListAsync(false, ct);
        return all.Where(x => guard.IsDoctor || !x.Definition.ClinicianOnly)
            .Select(x => new QuestionnaireSummaryDto(x.Definition.Key, x.Definition.Title, x.Definition.Description,
                x.Definition.ClinicianOnly, x.Definition.Items.Count, x.IsCustom)).ToList();
    }

    public async Task<QuestionnaireFormDto> GetFormAsync(string key, CancellationToken ct = default)
    {
        var def = await RequireDefinitionAsync(key, false, ct);
        return new QuestionnaireFormDto(def.Key, def.Title, def.Description, def.Prompt,
            QuestionnaireCatalog.DisclaimerText,
            def.Items.Select((it, i) => new QuestionItemDto(i, it.Text,
                it.Options.Select(o => new QuestionOptionDto(o.Value, o.Label)).ToList())).ToList());
    }

    public async Task<QuestionnaireResultDto> SubmitAsync(
        Guid? patientId, string key, IReadOnlyDictionary<int, int> answers, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        var def = await RequireDefinitionAsync(key, false, ct);
        var patient = await context.ResolveAsync(patientId, ct);

        if (answers.Count != def.Items.Count)
            throw new RequestValidationException("", "Responda todas as perguntas.");

        var values = new int[def.Items.Count];
        for (var i = 0; i < def.Items.Count; i++)
        {
            if (!answers.TryGetValue(i, out var v))
                throw new RequestValidationException("", "Responda todas as perguntas.");
            if (!def.Items[i].Options.Any(o => o.Value == v))
                throw new RequestValidationException("", $"Resposta inválida na pergunta {i + 1}.");
            values[i] = v;
        }

        var total = values.Sum();
        var response = new QuestionnaireResponse
        {
            OrganizationId = org, PatientId = patient.Id, QuestionnaireKey = def.Key,
            AnsweredAtUtc = clock.UtcNow, AnswersJson = JsonSerializer.Serialize(values),
            TotalScore = total, BandLabel = def.BandFor(total),
            SafetyFlag = def.SafetyItemIndex is int idx && values[idx] > 0,
            RecordedByUserId = user.UserId, RecordedByPatient = guard.IsPatient
        };
        await responses.AddAsync(response, ct);
        await uow.SaveChangesAsync(ct);
        // Auditoria sem pontuação nem respostas (podem revelar condição de saúde).
        await audit.RecordAsync(new AuditEntry("Questionnaire.Submit", nameof(QuestionnaireResponse), response.Id.ToString()), ct);
        if (response.SafetyFlag)
            await notifications.NotifyQuestionnaireSafetyFlagAsync(patient, response, def.Title, ct);
        return BuildResult(def, response);
    }

    public async Task<QuestionnaireResultDto> GetResultAsync(Guid id, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        var r = await responses.GetByIdAsync(id, ct);
        if (r is null || r.OrganizationId != org) throw new NotFoundException("Resultado não encontrado.");

        var patient = await context.ResolveAsync(r.PatientId, ct);
        if (patient.Id != r.PatientId) throw new NotFoundException("Resultado não encontrado."); // paciente tentando ver resultado de outro

        var def = await RequireDefinitionAsync(r.QuestionnaireKey, true, ct);
        await audit.RecordAsync(new AuditEntry("Questionnaire.View", nameof(QuestionnaireResponse), id.ToString()), ct);
        return BuildResult(def, r);
    }

    public async Task<IReadOnlyList<QuestionnaireResponseListItemDto>> ListResponsesAsync(
        Guid? patientId, string? key, int days, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var since = clock.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        var list = await responses.ListByPatientAsync(patient.Id, key, since, ct);
        await audit.RecordAsync(new AuditEntry("Questionnaire.List", nameof(Patient), patient.Id.ToString()), ct);

        var defs = (await definitions.ListAsync(true, ct)).ToDictionary(x => x.Definition.Key, x => x.Definition, StringComparer.OrdinalIgnoreCase);
        return list.OrderByDescending(r => r.AnsweredAtUtc).Select(r =>
        {
            defs.TryGetValue(r.QuestionnaireKey, out var def);
            return new QuestionnaireResponseListItemDto(r.Id, r.QuestionnaireKey, def?.Title ?? r.QuestionnaireKey,
                clock.ToLocal(r.AnsweredAtUtc), r.TotalScore, def?.MaxScore ?? r.TotalScore, r.BandLabel,
                r.SafetyFlag, r.RecordedByPatient);
        }).ToList();
    }

    private void RequireClinicalActor()
    {
        guard.RequireOrganization();
        if (!guard.IsDoctor && !guard.IsPatient) throw new ForbiddenException();
    }

    private async Task<QuestionnaireDefinition> RequireDefinitionAsync(string key, bool includeInactiveCustom, CancellationToken ct)
    {
        RequireClinicalActor();
        var def = await definitions.FindAsync(key, includeInactiveCustom, ct) ?? throw new NotFoundException("Instrumento não encontrado.");
        if (def.ClinicianOnly && !guard.IsDoctor) throw new ForbiddenException("Instrumento aplicado apenas pelo profissional.");
        return def;
    }

    private QuestionnaireResultDto BuildResult(QuestionnaireDefinition def, QuestionnaireResponse r)
    {
        var values = JsonSerializer.Deserialize<int[]>(r.AnswersJson) ?? [];
        var answers = def.Items.Select((it, i) =>
        {
            var v = i < values.Length ? values[i] : 0;
            return new AnswerViewDto(it.Text, it.Options.FirstOrDefault(o => o.Value == v)?.Label ?? v.ToString(), v);
        }).ToList();

        return new QuestionnaireResultDto(r.Id, r.PatientId, def.Key, def.Title, clock.ToLocal(r.AnsweredAtUtc),
            r.TotalScore, def.MaxScore, r.BandLabel, r.SafetyFlag, QuestionnaireCatalog.DisclaimerText, answers);
    }
}
