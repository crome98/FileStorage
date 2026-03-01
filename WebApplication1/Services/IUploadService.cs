namespace WebApplication1.Services;

public interface IUploadService
{
    CreateUploadResult CreateUpload(long uploadLength, string? filename, Stream? initialData);
    UploadProgressResult? GetProgress(string uploadId);
    Task<AppendChunkResult> AppendChunk(string uploadId, long clientOffset, Stream data);
    bool Terminate(string uploadId);
    long GetMaxUploadSize();
    void PurgeExpiredSessions();
}

public record CreateUploadResult(
    bool Success,
    string? UploadId = null,
    long? InitialOffset = null,
    string? Error = null
);

public record UploadProgressResult(long Offset, long Length, string Filename, DateTimeOffset ExpiresAt);

public record AppendChunkResult(
    bool Success,
    long? NewOffset = null,
    bool IsComplete = false,
    string? Error = null
);
