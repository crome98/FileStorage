# Requirements Specification: File Upload Endpoint (tus.io)

**Feature ID:** file-upload-endpoint
**Date:** 2026-02-22
**Status:** Complete

---

## Problem Statement

The API currently supports only file downloads (`GET /files/{filename}`). There is no way to get files onto the server. A file upload endpoint is needed that supports both single-request uploads (small files) and resumable chunked uploads (large files, e.g. 100 MB+), following the industry-standard **tus v1.0.0 protocol** so any standard tus client library works without custom integration code.

---

## Solution Overview

Implement the tus v1.0.0 resumable upload protocol on top of the existing ASP.NET Core minimal API. The implementation covers:
- Creating upload sessions (POST)
- Resuming uploads (PATCH)
- Checking upload progress (HEAD)
- Cancelling incomplete uploads (DELETE)
- Advertising server capabilities (OPTIONS)
- Automatic expiry of abandoned sessions

Uploaded files land in the same `./Files` directory used by the download endpoint, so a completed upload is immediately downloadable via `GET /files/{filename}`.

---

## Functional Requirements

### FR-1: Create Upload Session (POST)
- `POST /files/upload` creates a new upload resource
- Request must include `Tus-Resumable: 1.0.0` header; otherwise respond `412 Precondition Failed`
- Request must include `Upload-Length` header (total file size in bytes)
- Request may include `Upload-Metadata` header: a comma-separated list of base64-encoded key-value pairs. The `filename` key is used for the saved filename (decoded from base64). If absent, a UUID is used as the filename.
- If `Upload-Length` exceeds `FileStorage:MaxUploadSize`, respond `413 Request Entity Too Large`
- If `FileStorage:AllowOverwrite` is `false` and a file with the same filename already exists in `./Files`, respond `409 Conflict`
- Server creates a temp file at `{storagePath}/.uploads/{uploadId}.tmp`
- Server stores session state (in-memory): `uploadId → { Offset, Length, Filename, TempPath, CreatedAt }`
- Response: `201 Created`, headers: `Location: /files/upload/{uploadId}`, `Tus-Resumable: 1.0.0`

### FR-2: Creation-With-Upload (POST with body)
- If the POST request also includes `Content-Type: application/offset+octet-stream`, the body is treated as upload data (first/only chunk)
- Server appends the body bytes to the temp file immediately
- Response includes `Upload-Offset: {bytesAccepted}`
- This enables single-request upload for small files (no separate PATCH needed if entire file fits in one POST)

### FR-3: Resume / Append Chunk (PATCH)
- `PATCH /files/upload/{uploadId}` appends data to an in-progress upload
- Request must include: `Tus-Resumable: 1.0.0`, `Upload-Offset: {n}`, `Content-Type: application/offset+octet-stream`
- If upload ID is not found → `404 Not Found`
- If client `Upload-Offset` does not match server's stored offset → `409 Conflict`
- If `Content-Type` is not `application/offset+octet-stream` → `415 Unsupported Media Type`
- Server appends request body bytes to the temp file at the correct offset
- Updates stored offset: `offset += bytes written`
- Response: `204 No Content`, `Upload-Offset: {newOffset}`, `Tus-Resumable: 1.0.0`

### FR-4: Upload Completion (implicit on PATCH)
- When `Upload-Offset == Upload-Length` after a PATCH:
  - Move temp file from `{storagePath}/.uploads/{uploadId}.tmp` to `{storagePath}/{filename}`
  - If `FileStorage:AllowOverwrite` is `true`: overwrite existing file silently
  - If `FileStorage:AllowOverwrite` is `false`: respond `409 Conflict` (check should also happen at POST, but double-check here as a guard)
  - Remove session from in-memory state
  - Log: `"Upload complete: {Filename}, Size: {Size} bytes, UploadId: {UploadId}"`

### FR-5: Check Upload Progress (HEAD)
- `HEAD /files/upload/{uploadId}` returns current offset without modifying state
- If upload ID not found → `404 Not Found`
- Response: `200 OK`, headers: `Upload-Offset: {n}`, `Upload-Length: {total}`, `Cache-Control: no-store`, `Tus-Resumable: 1.0.0`

### FR-6: Cancel Upload (DELETE) — tus `termination` extension
- `DELETE /files/upload/{uploadId}` cancels an in-progress upload
- Deletes the temp file from disk
- Removes session from in-memory state
- If upload ID not found → `404 Not Found`
- Response: `204 No Content`, `Tus-Resumable: 1.0.0`

### FR-7: Server Capabilities (OPTIONS)
- `OPTIONS /files/upload` responds with server capabilities
- Response: `204 No Content`, headers:
  - `Tus-Resumable: 1.0.0`
  - `Tus-Version: 1.0.0`
  - `Tus-Extension: creation,creation-with-upload,termination,expiration`
  - `Tus-Max-Size: {FileStorage:MaxUploadSize}`

### FR-8: Session Expiry — tus `expiration` extension
- Incomplete sessions older than `FileStorage:UploadExpiryHours` (default: 24) are automatically expired
- On expiry: temp file deleted, session removed from in-memory state
- Expiry is enforced lazily: checked on each HEAD/PATCH request for the relevant session AND by a background `IHostedService` that sweeps every hour
- While a session is active, every PATCH and HEAD response includes `Upload-Expires: {RFC 7231 datetime}` header

### FR-9: Filename Validation
- The filename extracted from `Upload-Metadata` must pass the existing `IsValidFilename()` check (no `..`, `/`, `\`)
- If invalid → `400 Bad Request`
- Reuse `IFileService.IsValidFilename()` for this check

### FR-10: Overwrite Behaviour (configurable)
- Controlled by `FileStorage:AllowOverwrite` in `appsettings.json`
- `true` (default): overwrite silently on completion — S3-like behaviour
- `false`: reject with `409 Conflict` if a file with the same name exists

---

## Technical Requirements

### TR-1: New Files

| File | Purpose |
|------|---------|
| `WebApplication1/Services/IUploadService.cs` | Interface: `CreateUpload`, `AppendChunk`, `GetOffset`, `TerminateUpload`, `GetCapabilities` |
| `WebApplication1/Services/UploadService.cs` | Implementation with in-memory `ConcurrentDictionary<string, UploadSession>` |
| `WebApplication1.Tests/UploadServiceTests.cs` | xUnit tests following existing `FileServiceTests` pattern |

### TR-2: Modified Files

| File | Change |
|------|--------|
| `WebApplication1/Endpoints/FileEndpoints.cs` | Rename to `FilesEndpoints.cs`, rename class to `FilesEndpoints`, rename method to `MapFilesEndpoints()` |
| `WebApplication1/Program.cs` | Register `IUploadService` as Singleton; register expiry background service; call `app.MapFilesEndpoints()` |
| `WebApplication1/appsettings.json` | Add `FileStorage:MaxUploadSize`, `FileStorage:AllowOverwrite`, `FileStorage:UploadExpiryHours` |

### TR-3: IUploadService Interface
```csharp
public interface IUploadService
{
    CreateUploadResult CreateUpload(long uploadLength, string? filename, Stream? initialData);
    AppendChunkResult AppendChunk(string uploadId, long clientOffset, Stream data);
    UploadProgressResult? GetProgress(string uploadId);
    bool Terminate(string uploadId);
}

public record CreateUploadResult(bool Success, string? UploadId = null, long? InitialOffset = null, string? Error = null);
public record AppendChunkResult(bool Success, long? NewOffset = null, bool IsComplete = false, string? Error = null);
public record UploadProgressResult(long Offset, long Length, string Filename, DateTimeOffset ExpiresAt);
```

### TR-4: Upload Session Record
```csharp
internal record UploadSession(
    string UploadId,
    string Filename,
    string TempPath,
    long Length,
    long Offset,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);
```

### TR-5: Configuration Schema
```json
{
  "FileStorage": {
    "Path": "./Files",
    "MaxUploadSize": 5368709120,
    "AllowOverwrite": true,
    "UploadExpiryHours": 24
  }
}
```

### TR-6: Temp File Directory
- Path: `{storagePath}/.uploads/`
- Created automatically by `UploadService` constructor (same pattern as `FileService`)
- Temp files named `{uploadId}.tmp`
- On upload completion, moved (not copied) to `{storagePath}/{filename}`

### TR-7: Thread Safety
- `UploadService` registered as Singleton; must use `ConcurrentDictionary<string, UploadSession>` for session state
- File append operations must be serialized per uploadId (use a per-ID lock or `SemaphoreSlim`)

### TR-8: HTTP Request Body Streaming
- Chunk data must be streamed directly to the temp file — do not buffer the entire chunk in memory
- Use `Request.Body.CopyToAsync(fileStream)` pattern to avoid memory pressure for large files
- Disable ASP.NET Core's request body size limit for the PATCH endpoint (`[DisableRequestSizeLimit]` or `app.UseKestrel` config)

### TR-9: Background Expiry Service
```csharp
// WebApplication1/Services/UploadExpiryService.cs
public class UploadExpiryService : BackgroundService
{
    // Runs every hour, calls IUploadService to purge expired sessions
}
```
Registered in `Program.cs` via `builder.Services.AddHostedService<UploadExpiryService>()`.

### TR-10: Endpoint Registration
```csharp
// FilesEndpoints.cs
public static class FilesEndpoints
{
    public static WebApplication MapFilesEndpoints(this WebApplication app)
    {
        // Existing download route
        app.MapGet("/files/{filename}", DownloadFile)...;

        // New tus upload routes
        app.MapMethods("/files/upload", ["OPTIONS"], GetCapabilities)...;
        app.MapPost("/files/upload", CreateUpload)...;
        app.MapMethods("/files/upload/{uploadId}", ["HEAD"], GetProgress)...;
        app.MapPatch("/files/upload/{uploadId}", AppendChunk)...;
        app.MapDelete("/files/upload/{uploadId}", TerminateUpload)...;

        return app;
    }
}
```

---

## Configuration

Add to `appsettings.json`:
```json
{
  "FileStorage": {
    "Path": "./Files",
    "MaxUploadSize": 5368709120,
    "AllowOverwrite": true,
    "UploadExpiryHours": 24
  }
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FileStorage:MaxUploadSize` | long | 5368709120 (5 GB) | Max allowed `Upload-Length` in bytes |
| `FileStorage:AllowOverwrite` | bool | true | Overwrite existing files on upload completion |
| `FileStorage:UploadExpiryHours` | int | 24 | Hours before incomplete sessions auto-expire |

---

## Acceptance Criteria

- [ ] `OPTIONS /files/upload` returns correct `Tus-Extension` and `Tus-Max-Size`
- [ ] `POST /files/upload` with `Upload-Length` creates a session and returns `201` with `Location` header
- [ ] `POST /files/upload` with body data (creation-with-upload) writes bytes and returns `Upload-Offset`
- [ ] `PATCH /files/upload/{id}` appends bytes and returns updated `Upload-Offset`
- [ ] Final PATCH (offset == length) moves temp file to `./Files/{filename}` and file is downloadable via `GET /files/{filename}`
- [ ] `HEAD /files/upload/{id}` returns correct `Upload-Offset` and `Upload-Length`
- [ ] `DELETE /files/upload/{id}` removes session and temp file, returns `204`
- [ ] Wrong `Tus-Resumable` version returns `412`
- [ ] Offset mismatch on PATCH returns `409`
- [ ] `Upload-Length` exceeding `MaxUploadSize` returns `413`
- [ ] Invalid filename (path traversal) returns `400`
- [ ] Expired sessions are cleaned up automatically
- [ ] `AllowOverwrite: false` rejects duplicate filenames with `409`
- [ ] Large file upload (>100 MB) does not cause excessive memory usage (streaming)
- [ ] All new service methods covered by xUnit tests

---

## Assumptions

- No authentication or authorization is required on upload endpoints (consistent with existing download endpoint having no auth)
- The tus `checksum` extension is out of scope for this iteration
- The tus `concatenation` extension (merging multiple parallel uploads) is out of scope
- Kestrel max request body size limit must be disabled or raised to support large uploads
