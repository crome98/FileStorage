# FileStorage

An ASP.NET Core Web API solution targeting .NET 10.0 that provides file download and resumable file upload endpoints following the [tus.io v1.0.0](https://tus.io/protocols/resumable-upload) protocol.

## Solution Structure

```
FileStorage/
├── WebApplication1/               # Main web API project
│   ├── Endpoints/
│   │   └── FileEndpoints.cs       # All HTTP endpoint handlers
│   ├── Services/
│   │   ├── IFileService.cs        # File download interface
│   │   ├── FileService.cs         # File download implementation
│   │   ├── IUploadService.cs      # Upload interface
│   │   ├── UploadService.cs       # tus upload implementation
│   │   └── UploadExpiryService.cs # Background service for session cleanup
│   ├── TusConstants.cs            # Shared tus protocol constants
│   ├── Program.cs                 # App bootstrap and DI registration
│   └── appsettings.json           # Configuration
│
└── WebApplication1.Tests/         # Test project
    ├── FileServiceTests.cs        # Unit tests for FileService
    ├── UploadServiceTests.cs      # Unit tests for UploadService
    ├── FilesEndpointTests.cs      # Integration tests for all endpoints
    └── FilesEndpointRateLimitTests.cs # Integration tests for rate limiting
```

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Running the Application

```bash
# Build the solution
dotnet build

# Run the web API
dotnet run --project WebApplication1

# Run with watch mode (auto-rebuild on changes)
dotnet watch run --project WebApplication1
```

Default URLs:
- HTTP: `http://localhost:5155`
- HTTPS: `https://localhost:7095`

OpenAPI spec (Development only): `GET /openapi/v1.json`

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~UploadServiceTests"
dotnet test --filter "FullyQualifiedName~FilesEndpointTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~UploadServiceTests.CreateUpload_WithValidLength_ReturnsUploadId"
```

## Projects

### WebApplication1

ASP.NET Core Web API using the minimal API pattern. Endpoints are defined as extension methods in `Endpoints/FileEndpoints.cs` and registered in `Program.cs`.

**Key features:**
- `GET /files/{filename}` — download a stored file
- `OPTIONS /files/upload` — advertise tus server capabilities
- `POST /files/upload` — create a new upload session
- `PATCH /files/upload/{uploadId}` — append a chunk to an upload
- `HEAD /files/upload/{uploadId}` — check current upload progress
- `DELETE /files/upload/{uploadId}` — terminate an upload

See [`WebApplication1/README.md`](WebApplication1/README.md) for the full API reference and configuration options.

### WebApplication1.Tests

xUnit test project covering both the service layer (unit tests) and the HTTP layer (integration tests).

| File | Type | Coverage |
|------|------|----------|
| `FileServiceTests.cs` | Unit | File download service |
| `UploadServiceTests.cs` | Unit | Upload session lifecycle, chunk writing, expiry |
| `FilesEndpointTests.cs` | Integration | All tus endpoints end-to-end via `WebApplicationFactory` |
| `FilesEndpointRateLimitTests.cs` | Integration | Rate limit enforcement (HTTP 429) |

Integration tests spin up a full in-process test server using `WebApplicationFactory<Program>` and an isolated temp directory per test class, so no real files or shared state are affected.
