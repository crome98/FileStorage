# WebApplication1

An ASP.NET Core Web API targeting .NET 10.0, built with the minimal API pattern. It provides file download and resumable file upload endpoints using the [tus.io v1.0.0](https://tus.io/protocols/resumable-upload) protocol.

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started

```bash
# Run the application
dotnet run

# Run with watch mode (auto-rebuild on changes)
dotnet watch run
```

Default URLs:
- HTTP: `http://localhost:5155`
- HTTPS: `https://localhost:7095`

OpenAPI spec (Development only): `GET /openapi/v1.json`

## API Endpoints

### File Download

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/files/{filename}` | Download a file from storage |

**Responses**

| Status | Meaning |
|--------|---------|
| `200` | File content returned |
| `400` | Invalid filename (e.g. path traversal attempt) |
| `404` | File not found |

---

### Resumable Upload (tus v1.0.0)

All upload endpoints require the `Tus-Resumable: 1.0.0` request header. Missing or wrong version returns `412 Precondition Failed`.

| Method | Path | Description |
|--------|------|-------------|
| `OPTIONS` | `/files/upload` | Advertise server capabilities |
| `POST` | `/files/upload` | Create a new upload session |
| `PATCH` | `/files/upload/{uploadId}` | Append a chunk to an upload |
| `HEAD` | `/files/upload/{uploadId}` | Get current upload progress |
| `DELETE` | `/files/upload/{uploadId}` | Terminate and delete an upload |

#### OPTIONS — Advertise capabilities

Returns server capabilities. No request headers required.

**Response headers**

| Header | Example value |
|--------|---------------|
| `Tus-Resumable` | `1.0.0` |
| `Tus-Version` | `1.0.0` |
| `Tus-Extension` | `creation,creation-with-upload,termination,expiration` |
| `Tus-Max-Size` | `5368709120` |

#### POST — Create upload

**Request headers**

| Header | Required | Description |
|--------|----------|-------------|
| `Tus-Resumable` | Yes | Must be `1.0.0` |
| `Upload-Length` | Yes | Total file size in bytes |
| `Upload-Metadata` | No | Base64-encoded key-value pairs, e.g. `filename dGVzdC50eHQ=` |

**Responses**

| Status | Meaning |
|--------|---------|
| `201` | Upload created; `Location` header contains the upload URL |
| `400` | Missing/invalid `Upload-Length`, or invalid filename |
| `412` | Wrong `Tus-Resumable` version |
| `413` | Upload length exceeds `MaxUploadSize` |

#### PATCH — Append chunk

**Request headers**

| Header | Required | Description |
|--------|----------|-------------|
| `Tus-Resumable` | Yes | Must be `1.0.0` |
| `Upload-Offset` | Yes | Byte offset at which this chunk starts |
| `Content-Type` | Yes | Must be `application/offset+octet-stream` |

**Response headers (on success)**

| Header | Description |
|--------|-------------|
| `Tus-Resumable` | `1.0.0` |
| `Upload-Offset` | New byte offset after this chunk |

**Responses**

| Status | Meaning |
|--------|---------|
| `204` | Chunk accepted |
| `400` | Missing/invalid `Upload-Offset` header |
| `404` | Upload session not found |
| `409` | Client offset does not match server offset |
| `415` | Wrong `Content-Type` |

#### HEAD — Get progress

**Response headers**

| Header | Description |
|--------|-------------|
| `Tus-Resumable` | `1.0.0` |
| `Upload-Offset` | Bytes received so far |
| `Upload-Length` | Total expected bytes |
| `Cache-Control` | `no-store` |

**Responses**

| Status | Meaning |
|--------|---------|
| `200` | Progress returned in headers |
| `404` | Upload session not found |

#### DELETE — Terminate upload

**Responses**

| Status | Meaning |
|--------|---------|
| `204` | Upload terminated and temp file deleted |
| `404` | Upload session not found |

---

## Configuration

All settings are in `appsettings.json`.

```json
{
  "FileStorage": {
    "Path": "./Files",
    "MaxUploadSize": 5368709120,
    "AllowOverwrite": true,
    "UploadExpiryHours": 24
  },
  "RateLimit": {
    "PermitLimit": 100,
    "WindowMinutes": 1
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `FileStorage:Path` | `./Files` | Directory for completed files |
| `FileStorage:MaxUploadSize` | `5368709120` (5 GB) | Maximum allowed upload size in bytes |
| `FileStorage:AllowOverwrite` | `true` | Overwrite existing file on upload completion |
| `FileStorage:UploadExpiryHours` | `24` | Hours before an incomplete upload session expires |
| `RateLimit:PermitLimit` | `100` | Max upload requests per window |
| `RateLimit:WindowMinutes` | `1` | Rate limit window duration in minutes |

In-progress uploads are stored as temp files under `{FileStorage:Path}/.uploads/`. On completion they are moved atomically to `{FileStorage:Path}/`. Expired sessions are purged hourly by a background service.

## Project Structure

```
WebApplication1/
├── Endpoints/
│   └── FileEndpoints.cs      # All HTTP endpoint handlers
├── Services/
│   ├── IFileService.cs       # File download interface + result records
│   ├── FileService.cs        # File download implementation
│   ├── IUploadService.cs     # Upload interface + result records
│   ├── UploadService.cs      # tus upload implementation (in-memory sessions)
│   └── UploadExpiryService.cs# Background service: purges expired sessions hourly
├── TusConstants.cs           # Shared tus protocol constants
├── Program.cs                # App bootstrap, DI registration, rate limiter
└── appsettings.json          # Configuration

WebApplication1.Tests/
├── UploadServiceTests.cs     # Unit tests for UploadService
├── FilesEndpointTests.cs     # Integration tests for all endpoints
└── FilesEndpointRateLimitTests.cs # Integration tests for rate limiting
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~UploadServiceTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~UploadServiceTests.CreateUpload_WithValidLength_ReturnsUploadId"
```
