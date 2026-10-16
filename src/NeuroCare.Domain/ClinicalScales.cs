namespace NeuroCare.Domain;

public enum FallCircumstance { Walking = 1, Standing = 2, Transfer = 3, Stairs = 4, Bathroom = 5, Bed = 6, Other = 9 }

public class FallEvent : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public FallCircumstance Circumstance { get; set; } = FallCircumstance.Other;
    public string? Location { get; set; }
    public bool Injury { get; set; }
    public bool NeededMedicalCare { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool RecordedByPatient { get; set; }
}

/// <summary>Resposta a um instrumento do catálogo. A definição (perguntas/pontuação) fica no código; aqui só o resultado.</summary>
public class QuestionnaireResponse : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public string QuestionnaireKey { get; set; } = "";
    public DateTime AnsweredAtUtc { get; set; }
    /// <summary>Valores escolhidos, na ordem das perguntas (JSON: [0,1,2,...]).</summary>
    public string AnswersJson { get; set; } = "[]";
    public int TotalScore { get; set; }
    public string BandLabel { get; set; } = "";
    /// <summary>Resposta positiva ao item de segurança do instrumento (ex.: item 9 do PHQ-9).</summary>
    public bool SafetyFlag { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool RecordedByPatient { get; set; }
    public Patient? Patient { get; set; }
}
