namespace WebApplication1.Services;

public interface IFileService
{
    FileDownloadResult GetFile(string filename);
    string GetContentType(string filename);
    bool IsValidFilename(string filename);
}

public record FileDownloadResult(
    bool Success,
    string? FilePath = null,
    string? ContentType = null,
    long? FileSize = null,
    string? Error = null
);
