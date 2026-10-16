namespace NeuroCare.Application;

public record DetectedFileType(string ContentType, string[] Extensions);

/// <summary>Identifica o tipo pelo conteúdo (assinatura/“magic bytes”), não pelo nome ou Content-Type enviados.</summary>
public static class FileTypeDetector
{
    private static readonly DetectedFileType Pdf = new("application/pdf", [".pdf"]);
    private static readonly DetectedFileType Jpeg = new("image/jpeg", [".jpg", ".jpeg"]);
    private static readonly DetectedFileType Png = new("image/png", [".png"]);

    public static DetectedFileType? Detect(byte[] d)
    {
        if (d.Length >= 5 && d[0] == 0x25 && d[1] == 0x50 && d[2] == 0x44 && d[3] == 0x46 && d[4] == 0x2D) return Pdf;      // %PDF-
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return Jpeg;
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47
            && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A) return Png;
        return null;
    }
}
