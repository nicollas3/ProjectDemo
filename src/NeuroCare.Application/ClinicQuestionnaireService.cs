using System.Text.Json;
using System.Text.RegularExpressions;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IQuestionnaireDefinitionProvider
{
    Task<IReadOnlyList<(QuestionnaireDefinition Definition, bool IsCustom)>> ListAsync(bool includeInactiveCustom = false, CancellationToken ct = default);
    Task<QuestionnaireDefinition?> FindAsync(string key, bool includeInactiveCustom = false, CancellationToken ct = default);
}

public class QuestionnaireDefinitionProvider(IClinicQuestionnaireRepository repository, AccessGuard guard) : IQuestionnaireDefinitionProvider
{
    public async Task<IReadOnlyList<(QuestionnaireDefinition Definition, bool IsCustom)>> ListAsync(bool includeInactiveCustom = false, CancellationToken ct = default)
    {
        guard.RequireOrganization();
        var result = QuestionnaireCatalog.All.Select(x => (x, false)).ToList();
        var custom = await repository.ListAsync(includeInactiveCustom, ct);
        result.AddRange(custom.Where(x => includeInactiveCustom || x.Active).Select(x => (ToDefinition(x), true)));
        return result;
    }

    public async Task<QuestionnaireDefinition?> FindAsync(string key, bool includeInactiveCustom = false, CancellationToken ct = default)
    {
        guard.RequireOrganization();
        var builtIn = QuestionnaireCatalog.Find(key);
        if (builtIn is not null) return builtIn;
        var custom = await repository.GetByKeyAsync(key, includeInactiveCustom, ct);
        return custom is null ? null : ToDefinition(custom);
    }

    public static QuestionnaireDefinition ToDefinition(ClinicQuestionnaire q)
    {
        var items = JsonSerializer.Deserialize<List<QuestionItem>>(q.QuestionsJson) ?? [];
        var bands = JsonSerializer.Deserialize<List<ScoreBand>>(q.BandsJson) ?? [];
        return new QuestionnaireDefinition(q.Key, q.Title, q.Description, q.Prompt, q.ClinicianOnly, items, bands, q.SafetyItemIndex);
    }
}

public interface IClinicQuestionnaireService
{
    Task<IReadOnlyList<ClinicQuestionnaireListItemDto>> ListAsync(CancellationToken ct = default);
    Task<ClinicQuestionnaireEditDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(SaveClinicQuestionnaireDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, SaveClinicQuestionnaireDto dto, CancellationToken ct = default);
}

public class ClinicQuestionnaireService(
    IClinicQuestionnaireRepository repository, IUnitOfWork uow, AccessGuard guard, IAuditService audit) : IClinicQuestionnaireService
{
    public async Task<IReadOnlyList<ClinicQuestionnaireListItemDto>> ListAsync(CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var list = await repository.ListAsync(true, ct);
        return list.Select(q => new ClinicQuestionnaireListItemDto(q.Id, q.Key, q.Title, q.ClinicianOnly, q.Active,
            (JsonSerializer.Deserialize<List<QuestionItem>>(q.QuestionsJson) ?? []).Count)).ToList();
    }

    public async Task<ClinicQuestionnaireEditDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var q = await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException("Questionário não encontrado.");
        var items = JsonSerializer.Deserialize<List<QuestionItem>>(q.QuestionsJson) ?? [];
        var bands = JsonSerializer.Deserialize<List<ScoreBand>>(q.BandsJson) ?? [];
        var dto = new SaveClinicQuestionnaireDto
        {
            Key = q.Key, Title = q.Title, Description = q.Description, Prompt = q.Prompt,
            ClinicianOnly = q.ClinicianOnly, Active = q.Active,
            SafetyItemNumber = q.SafetyItemIndex.HasValue ? q.SafetyItemIndex + 1 : null,
            Items = items.Select(i => new ClinicQuestionItemInputDto
            {
                Text = i.Text,
                Options = string.Join(';', i.Options.Select(o => $"{o.Value}={o.Label}"))
            }).ToList(),
            Bands = bands.Select(b => new ScoreBandInputDto { Min = b.Min, Max = b.Max, Label = b.Label }).ToList()
        };
        return new ClinicQuestionnaireEditDto(id, dto);
    }

    public async Task<Guid> CreateAsync(SaveClinicQuestionnaireDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);
        var normalized = NormalizeKey(dto.Key);
        if (QuestionnaireCatalog.Find(normalized) is not null || await repository.KeyExistsAsync(normalized, null, ct))
            throw new RequestValidationException(nameof(dto.Key), "Esta chave já está em uso.");
        var q = new ClinicQuestionnaire { OrganizationId = org };
        Apply(dto, normalized, q);
        await repository.AddAsync(q, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("QuestionnaireDefinition.Create", nameof(ClinicQuestionnaire), q.Id.ToString()), ct);
        return q.Id;
    }

    public async Task UpdateAsync(Guid id, SaveClinicQuestionnaireDto dto, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);
        var q = await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException("Questionário não encontrado.");
        var normalized = NormalizeKey(dto.Key);
        if (!string.Equals(normalized, q.Key, StringComparison.OrdinalIgnoreCase))
            throw new RequestValidationException(nameof(dto.Key), "A chave não pode ser alterada depois que o questionário é criado.");
        Apply(dto, q.Key, q);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("QuestionnaireDefinition.Update", nameof(ClinicQuestionnaire), q.Id.ToString()), ct);
    }

    private static void Apply(SaveClinicQuestionnaireDto dto, string key, ClinicQuestionnaire q)
    {
        var items = ParseItems(dto.Items);
        var bands = ParseBands(dto.Bands, items);
        if (dto.SafetyItemNumber is < 1 || dto.SafetyItemNumber > items.Count)
            throw new RequestValidationException(nameof(dto.SafetyItemNumber), "Item de segurança fora do intervalo de perguntas.");

        q.Key = key;
        q.Title = dto.Title.Trim();
        q.Description = dto.Description.Trim();
        q.Prompt = dto.Prompt.Trim();
        q.ClinicianOnly = dto.ClinicianOnly;
        q.Active = dto.Active;
        q.QuestionsJson = JsonSerializer.Serialize(items);
        q.BandsJson = JsonSerializer.Serialize(bands);
        q.SafetyItemIndex = dto.SafetyItemNumber.HasValue ? dto.SafetyItemNumber.Value - 1 : null;
    }

    private static List<QuestionItem> ParseItems(List<ClinicQuestionItemInputDto> raw)
    {
        if (raw.Count == 0) throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Items), "Adicione ao menos uma pergunta.");
        if (raw.Count > 100) throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Items), "Limite de 100 perguntas.");
        var items = new List<QuestionItem>();
        foreach (var input in raw)
        {
            if (string.IsNullOrWhiteSpace(input.Text)) throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Items), "Toda pergunta precisa de texto.");
            var opts = new List<QuestionOption>();
            foreach (var token in (input.Options ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = token.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !int.TryParse(parts[0], out var value) || string.IsNullOrWhiteSpace(parts[1]))
                    throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Items), "Opções devem usar o formato 0=Não;1=Sim.");
                opts.Add(new QuestionOption(value, parts[1]));
            }
            if (opts.Count < 2 || opts.Select(o => o.Value).Distinct().Count() != opts.Count)
                throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Items), "Cada pergunta precisa de ao menos duas opções com valores distintos.");
            items.Add(new QuestionItem(input.Text.Trim(), opts));
        }
        return items;
    }

    private static List<ScoreBand> ParseBands(List<ScoreBandInputDto> raw, IReadOnlyList<QuestionItem> items)
    {
        var max = items.Sum(i => i.Options.Max(o => o.Value));
        var min = items.Sum(i => i.Options.Min(o => o.Value));
        if (raw.Count == 0) return [new ScoreBand(min, max, "Pontuação registrada")];
        var bands = raw.OrderBy(b => b.Min).Select(b => new ScoreBand(b.Min, b.Max, b.Label.Trim())).ToList();
        if (bands.Any(b => b.Min > b.Max || string.IsNullOrWhiteSpace(b.Label)))
            throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Bands), "Faixas de pontuação inválidas.");
        for (var i = 1; i < bands.Count; i++)
            if (bands[i].Min <= bands[i - 1].Max)
                throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Bands), "As faixas de pontuação não podem se sobrepor.");
        if (bands[0].Min > min || bands[^1].Max < max)
            throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Bands), $"As faixas devem cobrir toda a pontuação possível ({min} a {max}).");
        return bands;
    }

    private static string NormalizeKey(string value)
    {
        var key = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_-]+", "-").Trim('-');
        if (key.Length is < 3 or > 80) throw new RequestValidationException(nameof(SaveClinicQuestionnaireDto.Key), "Use uma chave de 3 a 80 caracteres.");
        return key;
    }
}
