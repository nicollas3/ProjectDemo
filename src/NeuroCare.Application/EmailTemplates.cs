using System.Net;

namespace NeuroCare.Application;

public static class EmailTemplates
{
    public static (string Subject, string Body) Invite(string name, string link) =>
        ("Seu acesso ao NeuroCare",
         $"<p>Olá, {H(name)}!</p><p>Você recebeu acesso ao NeuroCare. Defina sua senha pelo link abaixo (válido por tempo limitado):</p>" +
         $"<p><a href=\"{H(link)}\">Definir minha senha</a></p><p>Se você não esperava este e-mail, ignore-o.</p>");

    public static (string Subject, string Body) PasswordReset(string name, string link) =>
        ("Redefinição de senha - NeuroCare",
         $"<p>Olá, {H(name)}!</p><p>Recebemos um pedido para redefinir sua senha. Use o link abaixo (válido por tempo limitado):</p>" +
         $"<p><a href=\"{H(link)}\">Redefinir minha senha</a></p><p>Se você não fez o pedido, ignore este e-mail: sua senha continua a mesma.</p>");


    public static (string Subject, string Body) SecurityAlert(string patientName, string questionnaireTitle) =>
        ("Alerta de segurança - NeuroCare",
         $"<p>Foi registrada uma resposta que requer revisão profissional para o paciente <strong>{H(patientName)}</strong>.</p>" +
         $"<p>Instrumento: {H(questionnaireTitle)}.</p><p>Acesse o NeuroCare para revisar o registro. Por segurança, o conteúdo das respostas não é enviado por e-mail.</p>");

    public static (string Subject, string Body) AppointmentReminder(string patientName, DateTime localStart) =>
        ("Lembrete de consulta - NeuroCare",
         $"<p>Olá, {H(patientName)}!</p><p>Você possui uma consulta agendada para <strong>{localStart:dd/MM/yyyy 'às' HH:mm}</strong>.</p>" +
         "<p>Se houver alteração, entre em contato com sua clínica.</p>");

    public static (string Subject, string Body) MedicationReminder(string patientName, string medicationName, string dosage) =>
        ("Lembrete de medicação - NeuroCare",
         $"<p>Olá, {H(patientName)}!</p><p>Lembrete para sua medicação cadastrada: <strong>{H(medicationName)}</strong> — {H(dosage)}.</p>" +
         "<p>Siga a prescrição e as orientações do seu profissional de saúde. Este lembrete não altera a prescrição.</p>");

    private static string H(string s) => WebUtility.HtmlEncode(s);
}
