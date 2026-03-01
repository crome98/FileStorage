using System.Collections.Concurrent;

namespace WebApplication1.Services;

public class UploadService : IUploadService
{
    private readonly string _storagePath;
    private readonly long _maxUploadSize = long.MaxValue;
    private readonly IFileService _fileService;
    private readonly ConcurrentDictionary<string, UploadSession> _sessions = new();
    private readonly int _expiryHours;
    private readonly bool _allowOverwrite;

    public UploadService(IConfiguration configuration, IFileService fileService)
    {
        _fileService = fileService;
        var configuredPath = configuration.GetValue<string>("FileStorage:Path") ?? "./Files";
        _storagePath = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(Path.Combine(_storagePath, ".uploads"));
        if (!string.IsNullOrEmpty(configuration.GetValue<string>("FileStorage:MaxUploadSize")))
            long.TryParse(configuration.GetValue<string>("FileStorage:MaxUploadSize"), out _maxUploadSize);
        _expiryHours = configuration.GetValue<int>("FileStorage:UploadExpiryHours", 24);
        _allowOverwrite = configuration.GetValue<bool>("FileStorage:AllowOverwrite", true);
    }

    public CreateUploadResult CreateUpload(long uploadLength, string? filename, Stream? initialData)
    {
        if (uploadLength > _maxUploadSize)
            return new CreateUploadResult(Success: false, Error: "Upload length exceeds maximum allowed size");

        if (!string.IsNullOrEmpty(filename) && !_fileService.IsValidFilename(filename))
            return new CreateUploadResult(Success: false, Error: "Invalid filename");

        var uploadId = Guid.NewGuid().ToString("N");
        var resolvedFilename = string.IsNullOrEmpty(filename) ? uploadId : filename;
        var session = new UploadSession(
            UploadId: uploadId,
            Filename: resolvedFilename,
            TempPath: Path.Combine(_storagePath, ".uploads", $"{uploadId}.tmp"),
            Length: uploadLength,
            Offset: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(_expiryHours)
        );
        _sessions[uploadId] = session;
        return new CreateUploadResult(Success: true, UploadId: uploadId);
    }

    public UploadProgressResult? GetProgress(string uploadId)
    {
        if (!_sessions.TryGetValue(uploadId, out var session))
            return null;
        return new UploadProgressResult(session.Offset, session.Length, session.Filename, session.ExpiresAt);
    }

    public async Task<AppendChunkResult> AppendChunk(string uploadId, long clientOffset, Stream data)
    {
        if (!_sessions.TryGetValue(uploadId, out var session))
            return new AppendChunkResult(Success: false, Error: "Upload not found");

        if (clientOffset != session.Offset)
            return new AppendChunkResult(Success: false, Error: "Offset mismatch");

        await using var fileStream = new FileStream(session.TempPath, FileMode.Append, FileAccess.Write, FileShare.None);
        await data.CopyToAsync(fileStream);
        var bytesWritten = fileStream.Position - clientOffset;
        var newOffset = session.Offset + bytesWritten;

        _sessions[uploadId] = session with { Offset = newOffset };

        var isComplete = newOffset == session.Length;
        if (isComplete)
        {
            var finalPath = Path.Combine(_storagePath, session.Filename);
            File.Move(session.TempPath, finalPath, overwrite: _allowOverwrite);
            _sessions.TryRemove(uploadId, out _);
        }

        return new AppendChunkResult(Success: true, NewOffset: newOffset, IsComplete: isComplete);
    }

    public long GetMaxUploadSize() => _maxUploadSize;

    public void PurgeExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (uploadId, session) in _sessions)
        {
            if (session.ExpiresAt <= now)
            {
                _sessions.TryRemove(uploadId, out _);
                if (File.Exists(session.TempPath))
                    File.Delete(session.TempPath);
            }
        }
    }

    public bool Terminate(string uploadId)
    {
        if (!_sessions.TryRemove(uploadId, out UploadSession? session)) return false;

        if (File.Exists(session.TempPath))
        {
            File.Delete(session.TempPath);
        }

        return true;
    }
}

internal record UploadSession(
    string UploadId,
    string Filename,
    string TempPath,
    long Length,
    long Offset,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);
