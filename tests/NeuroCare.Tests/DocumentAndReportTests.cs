using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public sealed class FakeFileStorage : IFileStorage
{
    public long MaxFileBytes { get; set; } = 1024; // pequeno para facilitar o teste de limite
    public Dictionary<string, byte[]> Files { get; } = new();

    public Task SaveAsync(string key, byte[] content, CancellationToken ct = default)
    {
        Files[key] = content;
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var b) ? new MemoryStream(b) : null);
}

public class FileTypeDetectorTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 conteudo");
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] JpgBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];

    [Fact]
    public void Detects_allowed_types_by_content()
    {
        Assert.Equal("application/pdf", FileTypeDetector.Detect(PdfBytes)!.ContentType);
        Assert.Equal("image/png", FileTypeDetector.Detect(PngBytes)!.ContentType);
        Assert.Equal("image/jpeg", FileTypeDetector.Detect(JpgBytes)!.ContentType);
    }

    [Theory]
    [InlineData("MZ\u0090\u0000\u0003")]       // executável Windows
    [InlineData("<script>alert(1)</script>")]  // HTML/JS
    [InlineData("")]
    public void Rejects_other_content(string text) =>
        Assert.Null(FileTypeDetector.Detect(Encoding.Latin1.GetBytes(text)));
}

public class DocumentServiceTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 exame de teste");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static FakeCurrentUser Doctor(Scenario s) => FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId);
    private static FakeCurrentUser Patient1(Scenario s) => FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId);
    private static UploadDocumentDto Meta(string title = "Exame de sangue") => new() { Title = title, Category = DocumentCategory.Exam };
    private static Task<Guid> Upload(ClinicalEnv env, Guid? patient, byte[] data, string name = "exame.pdf", string title = "Exame de sangue") =>
        env.Documents.UploadAsync(patient, Meta(title), name, new MemoryStream(data));

    [Fact]
    public async Task Upload_stores_file_with_system_key_hash_and_can_be_downloaded()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var storage = new FakeFileStorage();
        using var env = new ClinicalEnv(db, Doctor(s), storage);

        var id = await Upload(env, s.PatientA1, Pdf);

        var doc = await env.Base.Db.PatientDocuments.SingleAsync(d => d.Id == id);
        Assert.Matches(new Regex("^[0-9a-f]{32}/[0-9a-f]{32}/[0-9a-f]{32}$"), doc.StorageKey);
        Assert.DoesNotContain("exame", doc.StorageKey);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf)).ToLowerInvariant(), doc.Sha256);
        Assert.Equal("application/pdf", doc.ContentType);
        Assert.Equal(Pdf.Length, doc.SizeBytes);

        var download = await env.Documents.OpenAsync(id);
        using var ms = new MemoryStream();
        await download.Content.CopyToAsync(ms);
        Assert.Equal(Pdf, ms.ToArray());
        Assert.Equal("exame.pdf", download.FileName);
    }

    [Fact]
    public async Task Invalid_files_are_rejected_and_nothing_is_stored()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var storage = new FakeFileStorage { MaxFileBytes = 64 };
        using var env = new ClinicalEnv(db, Doctor(s), storage);

        var exe = Encoding.Latin1.GetBytes("MZ\u0090\u0000 programa");
        await Assert.ThrowsAsync<RequestValidationException>(() => Upload(env, s.PatientA1, exe, "virus.pdf"));       // conteúdo != extensão
        await Assert.ThrowsAsync<RequestValidationException>(() => Upload(env, s.PatientA1, Png, "foto.pdf"));       // PNG disfarçado de PDF
        await Assert.ThrowsAsync<RequestValidationException>(() => Upload(env, s.PatientA1, [], "vazio.pdf"));
        await Assert.ThrowsAsync<RequestValidationException>(() => Upload(env, s.PatientA1, new byte[200].Select((_, i) => i == 0 ? (byte)'%' : (byte)'x').ToArray(), "grande.pdf")); // acima do limite
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Documents.UploadAsync(s.PatientA1, new UploadDocumentDto { Title = "" }, "a.pdf", new MemoryStream(Pdf)));

        Assert.Empty(storage.Files);
        Assert.Empty(await env.Base.Db.PatientDocuments.ToListAsync());
    }

    [Fact]
    public async Task Original_file_name_is_sanitized_and_png_is_accepted()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));

        await Upload(env, s.PatientA1, Png, "..\\..\\pasta/evil<>.png");

        var item = Assert.Single(await env.Documents.ListAsync(s.PatientA1));
        Assert.DoesNotContain("..", item.FileName);
        Assert.DoesNotContain("/", item.FileName);
        Assert.DoesNotContain("\\", item.FileName);
        Assert.EndsWith(".png", item.FileName);
    }

    [Fact]
    public async Task Patient_uploads_for_self_and_cannot_open_documents_of_others()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var storage = new FakeFileStorage();
        Guid a1Doc;
        using (var a1 = new ClinicalEnv(db, Patient1(s), storage))
        {
            a1Doc = await Upload(a1, s.PatientA2, Pdf); // tenta enviar para outro paciente: vai para o próprio
            Assert.Equal(s.PatientA1, (await a1.Base.Db.PatientDocuments.SingleAsync()).PatientId);
            Assert.True((await a1.Base.Db.PatientDocuments.SingleAsync()).UploadedByPatient);
            await Assert.ThrowsAsync<ForbiddenException>(() => a1.Documents.DeleteAsync(a1Doc));
        }

        using var a2 = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA2UserId), storage);
        await Assert.ThrowsAsync<NotFoundException>(() => a2.Documents.OpenAsync(a1Doc));
        Assert.Empty(await a2.Documents.ListAsync(null));
    }

    [Fact]
    public async Task Other_organization_and_clinic_admin_cannot_access()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var storage = new FakeFileStorage();
        Guid id;
        using (var doc = new ClinicalEnv(db, Doctor(s), storage))
            id = await Upload(doc, s.PatientA1, Pdf);

        using var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB), storage);
        await Assert.ThrowsAsync<NotFoundException>(() => b.Documents.OpenAsync(id));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Documents.ListAsync(s.PatientA1));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Documents.DeleteAsync(id));

        using var admin = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA), storage);
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Documents.ListAsync(s.PatientA1));
        await Assert.ThrowsAsync<ForbiddenException>(() => Upload(admin, s.PatientA1, Pdf));
    }

    [Fact]
    public async Task Doctor_soft_deletes_document_keeping_the_file_and_hiding_it()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var storage = new FakeFileStorage();
        using var env = new ClinicalEnv(db, Doctor(s), storage);
        var id = await Upload(env, s.PatientA1, Pdf);

        await env.Documents.DeleteAsync(id);

        Assert.Empty(await env.Documents.ListAsync(s.PatientA1));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Documents.OpenAsync(id));
        Assert.Single(storage.Files); // retenção: o arquivo físico permanece
        Assert.NotNull((await env.Base.Db.PatientDocuments.SingleAsync()).DeletedAtUtc);
    }

    [Fact]
    public async Task Audit_log_has_no_file_name_or_title_and_timeline_lists_document()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));
        var id = await Upload(env, s.PatientA1, Pdf, "laudo-confidencial.pdf", "TITULO-SIGILOSO");
        await env.Documents.OpenAsync(id);

        var logs = await env.Base.Db.AuditLogs.ToListAsync();
        Assert.Contains(logs, l => l.Action == "Document.Upload" && l.EntityId == id.ToString());
        Assert.Contains(logs, l => l.Action == "Document.Download");
        Assert.DoesNotContain(logs, l => (l.Details ?? "").Contains("SIGILOSO") || (l.Details ?? "").Contains("confidencial"));
        Assert.Contains(await env.Timeline.GetAsync(s.PatientA1), i => i.Kind == TimelineKind.Document);
    }
}

public class ReportServiceTests
{
    private static FakeCurrentUser Doctor(Scenario s) => FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId);

    [Fact]
    public async Task Report_consolidates_registered_data_only()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));

        // evolução assinada + rascunho (somente a assinada entra)
        var signed = await env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note("Relato assinado"));
        await env.Notes.SignAsync(signed);
        await env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note("Rascunho nao assinado"));
        // medicamentos: um ativo e um finalizado (somente o aberto entra)
        var m1 = await env.Medications.CreateAsync(s.PatientA1, new SaveMedicationDto { Name = "Levodopa", Dosage = "100 mg", Frequency = "3x", StartDate = new DateTime(2026, 1, 1) });
        var m2 = await env.Medications.CreateAsync(s.PatientA1, new SaveMedicationDto { Name = "Antigo", Dosage = "1 mg", Frequency = "1x", StartDate = new DateTime(2025, 1, 1) });
        await env.Medications.TransitionAsync(m2, MedicationAction.Finish);
        // sintomas, crise, queda, questionário
        foreach (var i in new[] { 4, 8 })
            await env.Symptoms.CreateAsync(s.PatientA1, new SaveSymptomDto { OccurredAtLocal = ClinicalEnv.Before(i), Type = SymptomType.Tremor, Intensity = i });
        await env.Seizures.CreateAsync(s.PatientA1, new SaveSeizureDto { OccurredAtLocal = ClinicalEnv.Before(), Type = SeizureType.Focal, DurationSeconds = 45 });
        await env.Falls.CreateAsync(s.PatientA1, new SaveFallDto { OccurredAtLocal = ClinicalEnv.Before(), Injury = true });
        await env.Questionnaires.SubmitAsync(s.PatientA1, "gad7", Enumerable.Range(0, 7).ToDictionary(i => i, _ => 1));

        var report = await env.Reports.GetPatientSummaryAsync(s.PatientA1, 90);

        Assert.Equal("Paciente A1", report.PatientName);
        Assert.Equal("***.***.789-09", report.MaskedCpf);
        var note = Assert.Single(report.Notes);
        Assert.Equal("Relato assinado", note.Subjective);
        var med = Assert.Single(report.Medications);
        Assert.Equal("Levodopa", med.Name);
        var symptom = Assert.Single(report.Symptoms);
        Assert.Equal(2, symptom.Count);
        Assert.Equal(6.0, symptom.AverageIntensity);
        Assert.Equal(8, symptom.MaxIntensity);
        Assert.Single(report.Seizures);
        Assert.Single(report.Falls);
        var scale = Assert.Single(report.Questionnaires);
        Assert.Equal(7, scale.Score);
        Assert.Equal("Leve", scale.Band);
        Assert.Null(scale.PreviousScore);
        _ = m1;
    }

    [Theory]
    [InlineData(Roles.Patient)]
    [InlineData(Roles.ClinicAdmin)]
    [InlineData(Roles.Researcher)]
    public async Task Only_doctors_can_generate_report(string role)
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(role, s.OrgA, role == Roles.Patient ? s.PatientA1UserId : null));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Reports.GetPatientSummaryAsync(s.PatientA1, 90));
    }

    [Fact]
    public async Task Doctor_of_another_organization_cannot_generate_report_and_generation_is_audited()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        using (var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB)))
            await Assert.ThrowsAsync<NotFoundException>(() => b.Reports.GetPatientSummaryAsync(s.PatientA1, 90));

        using var env = new ClinicalEnv(db, Doctor(s));
        await env.Reports.GetPatientSummaryAsync(s.PatientA1, 90);
        var log = await env.Base.Db.AuditLogs.SingleAsync(l => l.Action == "Report.Generate");
        Assert.Equal(s.PatientA1.ToString(), log.EntityId);
        Assert.Null(log.Details);
    }
}
