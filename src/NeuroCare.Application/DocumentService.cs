using System.Security.Cryptography;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentListItemDto>> ListAsync(Guid? patientId, CancellationToken ct = default);
    Task<Guid> UploadAsync(Guid? patientId, UploadDocumentDto dto, string fileName, Stream content, CancellationToken ct = default);
    Task<DocumentDownload> OpenAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Documentos do paciente (PDF/JPG/PNG). Validação por conteúdo e tamanho, nome de arquivo nunca usado como caminho,
/// acesso por organização + (médico ou próprio paciente). Exclusão é lógica (somente médico).
/// </summary>
public class DocumentService(
    ClinicalContext context, IDocumentRepository documents, IFileStorage storage, IUnitOfWork uow,
    AccessGuard guard, ICurrentUser user, IAuditService audit, IClock clock) : IDocumentService
{
    public async Task<IReadOnlyList<DocumentListItemDto>> ListAsync(Guid? patientId, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var list = await documents.ListByPatientAsync(patient.Id, ct);
        await audit.RecordAsync(new AuditEntry("Document.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.Select(d => new DocumentListItemDto(d.Id, d.Title, d.Category, d.OriginalFileName, d.SizeBytes,
            clock.ToLocal(d.CreatedAt), d.UploadedByPatient)).ToList();
    }

    public async Task<Guid> UploadAsync(Guid? patientId, UploadDocumentDto dto, string fileName, Stream content, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        DtoValidator.EnsureValid(dto);
        var patient = await context.ResolveAsync(patientId, ct);

        var data = await ReadLimitedAsync(content, storage.MaxFileBytes, ct);
        if (data.Length == 0) throw new RequestValidationException("File", "O arquivo está vazio.");

        var type = FileTypeDetector.Detect(data)
                   ?? throw new RequestValidationException("File", "Tipo de arquivo não permitido. Envie PDF, JPG ou PNG.");
        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (!type.Extensions.Contains(extension))
            throw new RequestValidationException("File", "A extensão do arquivo não corresponde ao seu conteúdo.");

        // Chave gerada pelo sistema: o nome enviado pelo usuário nunca vira caminho no disco.
        var key = $"{org:N}/{patient.Id:N}/{Guid.NewGuid():N}";
        await storage.SaveAsync(key, data, ct);

        var doc = new PatientDocument
        {
            OrganizationId = org, PatientId = patient.Id, Title = dto.Title.Trim(), Category = dto.Category,
            OriginalFileName = SanitizeFileName(fileName ?? "documento" + extension), ContentType = type.ContentType,
            SizeBytes = data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
            StorageKey = key, UploadedByUserId = user.UserId, UploadedByPatient = guard.IsPatient
        };
        await documents.AddAsync(doc, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Document.Upload", nameof(PatientDocument), doc.Id.ToString()), ct);
        return doc.Id;
    }

    public async Task<DocumentDownload> OpenAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await LoadAsync(id, ct);
        var patient = await context.ResolveAsync(doc.PatientId, ct);
        if (patient.Id != doc.PatientId) throw new NotFoundException("Documento não encontrado."); // paciente tentando abrir de outro

        var stream = await storage.OpenReadAsync(doc.StorageKey, ct)
                     ?? throw new NotFoundException("Arquivo não encontrado no armazenamento.");
        await audit.RecordAsync(new AuditEntry("Document.Download", nameof(PatientDocument), id.ToString()), ct);
        return new DocumentDownload(stream, doc.OriginalFileName, doc.ContentType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var doc = await LoadAsync(id, ct);
        doc.DeletedAtUtc = clock.UtcNow;
        doc.DeletedByUserId = user.UserId;
        await uow.SaveChangesAsync(ct); // arquivo físico é mantido (retenção); apenas oculto
        await audit.RecordAsync(new AuditEntry("Document.Delete", nameof(PatientDocument), id.ToString()), ct);
    }

    private async Task<PatientDocument> LoadAsync(Guid id, CancellationToken ct)
    {
        var org = guard.RequireOrganization();
        var doc = await documents.GetByIdAsync(id, ct);
        if (doc is null || doc.OrganizationId != org || doc.DeletedAtUtc.HasValue)
            throw new NotFoundException("Documento não encontrado.");
        return doc;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, long max, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (ms.Length > max)
                throw new RequestValidationException("File", $"O arquivo excede o limite de {max / (1024 * 1024)} MB.");
        }
        return ms.ToArray();
    }

    internal static string SanitizeFileName(string name)
    {
        var onlyName = Path.GetFileName(name.Replace('\\', '/'));
        var clean = new string(onlyName.Where(c => !char.IsControl(c) && "<>:\"/\\|?*".IndexOf(c) < 0).ToArray()).Trim();
        if (clean.Length == 0) clean = "documento";
        return clean.Length <= 150 ? clean : clean[^150..];
    }
}
