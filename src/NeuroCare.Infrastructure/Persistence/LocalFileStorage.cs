using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

/// <summary>
/// Armazenamento em disco FORA de wwwroot (nunca servido como arquivo estático). Configure Storage:RootPath
/// com um caminho absoluto em volume persistente e com backup. Os arquivos NÃO são criptografados pela aplicação:
/// use criptografia de disco/volume. Não há antivírus embutido.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    // Formato gerado pelo sistema: {org}/{paciente}/{arquivo}, cada parte um GUID "N" (32 hex).
    private static readonly Regex KeyPattern = new("^[0-9a-f]{32}/[0-9a-f]{32}/[0-9a-f]{32}$", RegexOptions.Compiled);
    private readonly string _root;

    public long MaxFileBytes { get; }

    public LocalFileStorage(IConfiguration config)
    {
        var configured = config["Storage:RootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads")
            : configured);
        MaxFileBytes = config.GetValue("Storage:MaxFileBytes", 10L * 1024 * 1024);
    }

    public async Task SaveAsync(string key, byte[] content, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    private string ResolvePath(string key)
    {
        if (!KeyPattern.IsMatch(key)) throw new ArgumentException("Chave de armazenamento inválida.", nameof(key));
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Chave de armazenamento inválida.", nameof(key));
        return full;
    }
}
