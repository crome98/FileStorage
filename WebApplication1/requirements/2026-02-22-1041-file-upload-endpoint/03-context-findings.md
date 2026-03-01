# Context Findings

## tus.io v1.0.0 Protocol Summary

### Required HTTP Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| OPTIONS | /files/upload | Advertise server capabilities |
| POST   | /files/upload | Create a new upload session, get upload URL |
| HEAD   | /files/upload/{id} | Query current offset (for resume) |
| PATCH  | /files/upload/{id} | Send chunk data at a given offset |
| DELETE | /files/upload/{id} | Cancel upload (termination extension) |

### Key Headers

Every request (except OPTIONS) must include `Tus-Resumable: 1.0.0`. If the version doesn't match, server returns **412 Precondition Failed**.

**POST (Create):**
- Client sends: `Upload-Length` (total file size), optional `Upload-Metadata` (base64 key-value pairs for filename, filetype, etc.)
- Server returns: `201 Created`, `Location: /files/upload/{id}`

**Creation-With-Upload (POST + data in one request):**
- Client also sends `Content-Type: application/offset+octet-stream` and the file bytes in the POST body
- Server returns `Upload-Offset` in response to indicate bytes accepted
- Enables single-request upload for small files (normal upload)

**PATCH (Send chunk):**
- Client sends: `Upload-Offset` (where to resume), `Content-Type: application/offset+octet-stream`, raw bytes in body
- Server returns: `204 No Content`, updated `Upload-Offset`
- Error **409 Conflict** if client offset doesn't match server offset

**HEAD (Check progress):**
- Client sends: `Tus-Resumable: 1.0.0`
- Server returns: `Upload-Offset`, `Upload-Length`, `Cache-Control: no-store`

**OPTIONS (Capabilities):**
- Server returns: `Tus-Version: 1.0.0`, `Tus-Extension`, `Tus-Max-Size`

### tus Extensions Relevant Here

- `creation` — Required; POST to create upload resources
- `creation-with-upload` — Allows single-request normal upload via POST
- `termination` — Allows DELETE to cancel incomplete uploads
- `expiration` — Server automatically expires stale sessions (`Upload-Expires` header)

### Upload Completion
Server finalizes the upload implicitly when `Upload-Offset == Upload-Length`. At that point, the temp file is moved/renamed to the storage directory.

### Error Codes
| Code | Condition |
|------|-----------|
| 412 | Wrong `Tus-Resumable` version |
| 409 | Offset mismatch on PATCH |
| 413 | Upload exceeds `Tus-Max-Size` |
| 415 | Wrong `Content-Type` on PATCH |
| 404 | Upload ID not found |

---

## Codebase Patterns

### Files That Need Modification
1. **`WebApplication1/Services/IFileService.cs`** — Add upload-related methods OR create a new `IUploadService` interface
2. **`WebApplication1/Services/FileService.cs`** — Implement upload methods OR create a new `UploadService`
3. **`WebApplication1/Endpoints/FileEndpoints.cs`** — Add tus endpoints OR create `UploadEndpoints.cs`
4. **`WebApplication1/Program.cs`** — Register new service + call new endpoint mapper
5. **`WebApplication1/appsettings.json`** — Add `FileStorage:MaxUploadSize`, `FileStorage:AllowOverwrite`
6. **`WebApplication1.Tests/FileServiceTests.cs`** — Add upload service tests OR create `UploadServiceTests.cs`

### Architecture Pattern to Follow
- New `IUploadService` / `UploadService` pair in `Services/`
- New `UploadEndpoints.cs` in `Endpoints/` with `MapUploadEndpoints()` extension method
- Registered as Singleton in `Program.cs`
- Configuration read via `IConfiguration` in constructor

### Configuration to Add (appsettings.json)
```json
{
  "FileStorage": {
    "Path": "./Files",
    "MaxUploadSize": 5368709120,
    "AllowOverwrite": true
  }
}
```

### Upload Session State
The tus protocol requires the server to track the upload session between requests:
- `uploadId` → generated GUID
- Current `Upload-Offset`
- `Upload-Length`
- Original filename (from `Upload-Metadata`)
- Temp file path for in-progress chunks

**Storage options for session state:**
- **In-memory dictionary** (simplest, state lost on restart)
- **On-disk JSON file** alongside temp file (survives restart, more durable)

### Temp Files Pattern
Chunks written to a temp file at `{storagePath}/.uploads/{uploadId}.tmp` during upload. On completion, moved to `{storagePath}/{filename}`.

### Existing Service Interface for Reference
```csharp
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
```

### Test Pattern to Follow
```csharp
public class UploadServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly UploadService _sut;

    public UploadServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:Path"] = _testDirectory,
                ["FileStorage:MaxUploadSize"] = "104857600",
                ["FileStorage:AllowOverwrite"] = "true"
            })
            .Build();
        _sut = new UploadService(configuration);
    }

    public void Dispose() => Directory.Delete(_testDirectory, recursive: true);
}
```
