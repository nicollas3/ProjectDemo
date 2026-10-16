namespace NeuroCare.Application;

public record ConsentPurposeDefinition(string Key, string Version, string Text);

/// <summary>
/// Textos versionados de consentimento. Alterar o texto exige nova versão.
/// A definição de base legal continua sendo responsabilidade jurídica da organização.
/// </summary>
public static class ConsentPurposeCatalog
{
    public static IReadOnlyList<ConsentPurposeDefinition> All { get; } =
    [
        new("notifications-email", "1.0",
            "Autorizo o envio de lembretes de consultas e de medicações por e-mail. Sei que posso revogar esta autorização a qualquer momento no portal.")
    ];

    public static ConsentPurposeDefinition? Find(string key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
