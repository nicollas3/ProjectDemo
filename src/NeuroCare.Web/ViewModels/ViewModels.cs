using System.ComponentModel.DataAnnotations;
using NeuroCare.Application;

namespace NeuroCare.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe a senha."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Senha atual")]
    public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Confirmar nova senha"), Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem.")]
    public string ConfirmPassword { get; set; } = "";
}

public record DashboardViewModel(string Greeting, DashboardDto Data);
public record AppointmentsIndexViewModel(IReadOnlyList<AppointmentListItemDto> Items, DateTime From, DateTime To);

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = "";
}

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }
    [Required] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Informe a nova senha."), DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Confirme a nova senha."), DataType(DataType.Password), Display(Name = "Confirmar nova senha"),
     Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem.")]
    public string ConfirmPassword { get; set; } = "";
}

public class QuestionnaireAnswersInput
{
    /// <summary>Índice da pergunta → valor escolhido (name="Answers[i]" nos radios).</summary>
    public Dictionary<int, int> Answers { get; set; } = new();
}

public record QuestionnairesIndexViewModel(
    IReadOnlyList<QuestionnaireSummaryDto> Available,
    IReadOnlyList<QuestionnaireResponseListItemDto> Recent);
