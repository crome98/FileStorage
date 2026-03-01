namespace WebApplication1.Services;

public class FileService : IFileService
{
    private readonly string _storagePath;

    public FileService(IConfiguration configuration)
    {
        var configuredPath = configuration.GetValue<string>("FileStorage:Path") ?? "./Files";
        _storagePath = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(_storagePath);
    }

    public bool IsValidFilename(string filename)
    {
        return !filename.Contains("..")
            && !filename.Contains('/')
            && !filename.Contains('\\');
    }

    public FileDownloadResult GetFile(string filename)
    {
        if (!IsValidFilename(filename))
        {
            return new FileDownloadResult(false, Error: "Invalid filename");
        }

        var filePath = Path.Combine(_storagePath, filename);

        if (!File.Exists(filePath))
        {
            return new FileDownloadResult(false, Error: "File not found");
        }

        var fileInfo = new FileInfo(filePath);
        var contentType = GetContentType(filename);

        return new FileDownloadResult(
            Success: true,
            FilePath: filePath,
            ContentType: contentType,
            FileSize: fileInfo.Length
        );
    }

    public string GetContentType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
