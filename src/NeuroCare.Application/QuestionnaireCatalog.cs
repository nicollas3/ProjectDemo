namespace NeuroCare.Application;

public record QuestionOption(int Value, string Label);
public record QuestionItem(string Text, IReadOnlyList<QuestionOption> Options);
public record ScoreBand(int Min, int Max, string Label);

public record QuestionnaireDefinition(
    string Key, string Title, string Description, string Prompt, bool ClinicianOnly,
    IReadOnlyList<QuestionItem> Items, IReadOnlyList<ScoreBand> Bands, int? SafetyItemIndex)
{
    public int MaxScore => Items.Sum(i => i.Options.Max(o => o.Value));
    public string BandFor(int score) => Bands.FirstOrDefault(b => score >= b.Min && score <= b.Max)?.Label ?? "—";
}

/// <summary>
/// Catálogo de instrumentos. Inclui apenas escalas de uso livre. NÃO inclui instrumentos protegidos
/// por direitos autorais/licença (ex.: MDS-UPDRS, MoCA, MMSE). Escalas licenciadas devem ser adicionadas
/// somente com autorização do detentor dos direitos.
/// </summary>
public static class QuestionnaireCatalog
{
    private const string Disclaimer =
        "Instrumento de acompanhamento/triagem. A pontuação não constitui diagnóstico; a interpretação é do profissional de saúde.";

    private static readonly QuestionOption[] Frequency =
    [
        new(0, "Nenhum dia"), new(1, "Vários dias"), new(2, "Mais da metade dos dias"), new(3, "Quase todos os dias")
    ];

    private static QuestionItem F(string text) => new(text, Frequency);

    public static readonly QuestionnaireDefinition Phq9 = new(
        "phq9", "PHQ-9 – Humor", "Rastreio e acompanhamento de sintomas depressivos (9 perguntas).",
        "Nas últimas 2 semanas, com que frequência você foi incomodado(a) pelos problemas abaixo?", false,
        [
            F("Pouco interesse ou pouco prazer em fazer as coisas"),
            F("Sentir-se para baixo, deprimido(a) ou sem perspectiva"),
            F("Dificuldade para pegar no sono ou permanecer dormindo, ou dormir mais do que de costume"),
            F("Sentir-se cansado(a) ou com pouca energia"),
            F("Falta de apetite ou comer demais"),
            F("Sentir-se mal consigo mesmo(a), ou achar que é um fracasso ou que decepcionou sua família ou a si mesmo(a)"),
            F("Dificuldade para se concentrar nas coisas, como ler o jornal ou ver televisão"),
            F("Lentidão para se movimentar ou falar, a ponto de outras pessoas perceberem; ou o oposto: estar tão agitado(a) ou inquieto(a) que você fica andando de um lado para o outro mais do que de costume"),
            F("Pensar em se ferir de alguma maneira ou que seria melhor estar morto(a)")
        ],
        [new(0, 4, "Mínima"), new(5, 9, "Leve"), new(10, 14, "Moderada"), new(15, 19, "Moderadamente grave"), new(20, 27, "Grave")],
        SafetyItemIndex: 8);

    public static readonly QuestionnaireDefinition Gad7 = new(
        "gad7", "GAD-7 – Ansiedade", "Rastreio e acompanhamento de sintomas de ansiedade (7 perguntas).",
        "Nas últimas 2 semanas, com que frequência você foi incomodado(a) pelos problemas abaixo?", false,
        [
            F("Sentir-se nervoso(a), ansioso(a) ou muito tenso(a)"),
            F("Não ser capaz de impedir ou de controlar as preocupações"),
            F("Preocupar-se muito com diversas coisas"),
            F("Dificuldade para relaxar"),
            F("Ficar tão agitado(a) que se torna difícil permanecer sentado(a)"),
            F("Ficar facilmente aborrecido(a) ou irritado(a)"),
            F("Sentir medo como se algo horrível fosse acontecer")
        ],
        [new(0, 4, "Mínima"), new(5, 9, "Leve"), new(10, 14, "Moderada"), new(15, 21, "Grave")],
        SafetyItemIndex: null);

    public static readonly QuestionnaireDefinition Mrs = new(
        "mrs", "Rankin modificada (mRS) – Funcionalidade pós-AVC", "Grau de incapacidade funcional (aplicada pelo profissional).",
        "Selecione o nível que melhor descreve a condição atual do paciente.", true,
        [
            new("Nível funcional", new QuestionOption[]
            {
                new(0, "0 – Sem sintomas"),
                new(1, "1 – Sem incapacidade significativa apesar dos sintomas: realiza todas as atividades habituais"),
                new(2, "2 – Incapacidade leve: não realiza todas as atividades anteriores, mas cuida de si sem ajuda"),
                new(3, "3 – Incapacidade moderada: precisa de alguma ajuda, mas caminha sem assistência"),
                new(4, "4 – Incapacidade moderadamente grave: não caminha sem assistência nem atende às próprias necessidades sem ajuda"),
                new(5, "5 – Incapacidade grave: acamado(a), incontinente, requer cuidados constantes")
            })
        ],
        [new(0, 0, "Sem sintomas"), new(1, 1, "Sem incapacidade significativa"), new(2, 2, "Incapacidade leve"),
         new(3, 3, "Incapacidade moderada"), new(4, 4, "Incapacidade moderadamente grave"), new(5, 5, "Incapacidade grave")],
        SafetyItemIndex: null);

    public static readonly QuestionnaireDefinition HoehnYahr = new(
        "hoehnyahr", "Hoehn e Yahr (simplificada) – Estadiamento na doença de Parkinson",
        "Estágio da doença, sem estágios intermediários (aplicada pelo profissional).",
        "Selecione o estágio que melhor descreve a condição atual do paciente.", true,
        [
            new("Estágio", new QuestionOption[]
            {
                new(1, "Estágio 1 – Doença unilateral"),
                new(2, "Estágio 2 – Doença bilateral, sem comprometimento do equilíbrio"),
                new(3, "Estágio 3 – Doença bilateral leve a moderada, com alguma instabilidade postural; fisicamente independente"),
                new(4, "Estágio 4 – Incapacidade grave; ainda consegue caminhar ou ficar em pé sem ajuda"),
                new(5, "Estágio 5 – Confinado(a) à cadeira de rodas ou ao leito, a menos que receba ajuda")
            })
        ],
        [new(1, 1, "Estágio 1"), new(2, 2, "Estágio 2"), new(3, 3, "Estágio 3"), new(4, 4, "Estágio 4"), new(5, 5, "Estágio 5")],
        SafetyItemIndex: null);

    public static IReadOnlyList<QuestionnaireDefinition> All { get; } = [Phq9, Gad7, Mrs, HoehnYahr];

    public static string DisclaimerText => Disclaimer;

    public static QuestionnaireDefinition? Find(string? key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
}
