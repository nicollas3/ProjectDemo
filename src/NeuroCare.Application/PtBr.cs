using NeuroCare.Domain;

namespace NeuroCare.Application;

/// <summary>Rótulos em português (pt-BR) dos enums exibidos ao usuário.</summary>
public static class PtBr
{
    public static string Label(this AppointmentStatus v) => v switch
    {
        AppointmentStatus.Scheduled => "Agendada", AppointmentStatus.Confirmed => "Confirmada",
        AppointmentStatus.Completed => "Realizada", AppointmentStatus.Cancelled => "Cancelada",
        AppointmentStatus.NoShow => "Faltou", _ => v.ToString()
    };

    public static string Label(this AppointmentType v) => v switch
    {
        AppointmentType.FirstVisit => "Primeira consulta", AppointmentType.FollowUp => "Acompanhamento",
        AppointmentType.Return => "Retorno", AppointmentType.Telemedicine => "Telemedicina",
        AppointmentType.Exam => "Exame", _ => v.ToString()
    };

    public static string Label(this NoteStatus v) => v == NoteStatus.Signed ? "Assinada" : "Rascunho";

    public static string Label(this MedicationStatus v) => v switch
    {
        MedicationStatus.Active => "Em uso", MedicationStatus.Suspended => "Suspenso", _ => "Finalizado"
    };

    public static string Label(this SymptomType v) => v switch
    {
        SymptomType.Tremor => "Tremor", SymptomType.Rigidity => "Rigidez",
        SymptomType.Bradykinesia => "Lentidão dos movimentos", SymptomType.Headache => "Dor de cabeça",
        SymptomType.Dizziness => "Tontura", SymptomType.Weakness => "Fraqueza",
        SymptomType.SpeechChange => "Alteração da fala", SymptomType.VisionChange => "Alteração da visão",
        SymptomType.Numbness => "Formigamento/dormência", SymptomType.Memory => "Alteração de memória",
        SymptomType.Sleep => "Alteração do sono", _ => "Outro"
    };

    public static string Label(this SeizureType v) => v switch
    {
        SeizureType.Focal => "Focal", SeizureType.GeneralizedTonicClonic => "Tônico-clônica generalizada",
        SeizureType.Absence => "Ausência", SeizureType.Myoclonic => "Mioclônica",
        SeizureType.Other => "Outro", _ => "Não sabe informar"
    };

    public static string Label(this FallCircumstance v) => v switch
    {
        FallCircumstance.Walking => "Caminhando", FallCircumstance.Standing => "Em pé parado(a)",
        FallCircumstance.Transfer => "Ao levantar/sentar ou transferir-se", FallCircumstance.Stairs => "Escadas ou degraus",
        FallCircumstance.Bathroom => "No banheiro", FallCircumstance.Bed => "Da cama", _ => "Outra situação"
    };

    public static string Label(this DocumentCategory v) => v switch
    {
        DocumentCategory.Exam => "Exame", DocumentCategory.Report => "Laudo", DocumentCategory.Prescription => "Receita",
        DocumentCategory.Imaging => "Imagem", _ => "Outro"
    };
}
