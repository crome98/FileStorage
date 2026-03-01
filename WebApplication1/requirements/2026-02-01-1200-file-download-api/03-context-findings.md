# Context Findings

## Codebase Analysis

### Current Architecture
- **Framework:** ASP.NET Core Minimal API on .NET 10.0
- **Entry Point:** `Program.cs` using minimal API pattern (no controllers)
- **Existing Endpoint:** `GET /weatherforecast` as example
- **OpenAPI:** Enabled via `Microsoft.AspNetCore.OpenApi` package

### Files to Modify
- `Program.cs` - Add new file download endpoint(s)

### Patterns to Follow
Based on existing code:
- Use `app.MapGet()` for defining endpoints
- Use `.WithName()` for OpenAPI operation naming
- Keep endpoints in `Program.cs` (small project)

## Best Practices Research

### File Download Implementation in Minimal APIs
From [DEV Community guide](https://dev.to/leandroveiga/mastering-file-uploads-and-downloads-in-net-8-minimal-apis-a-comprehensive-guide-5al3):
- Use `Results.File()` or `TypedResults.File()` to return files
- Set appropriate MIME type (`application/octet-stream` for generic downloads)
- Stream large files to reduce memory usage

### Security Considerations
- Store files in directories with limited permissions
- Avoid placing downloadable files in public wwwroot directory
- Validate file paths to prevent directory traversal attacks
- Consider authentication for sensitive files (future requirement)

### Code Organization
From [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0) and [Milan Jovanovic](https://www.milanjovanovic.tech/blog/how-to-structure-minimal-apis):
- Use extension methods to organize endpoints as project grows
- Use `TypedResults` for better OpenAPI documentation and compile-time safety
- Separate API contracts (DTOs) from domain models

### Logging
- Use built-in `ILogger<T>` for download tracking
- Log: timestamp, file name, file size, client IP (for future auth: user ID)

## Technical Constraints
- Local filesystem storage initially
- No authentication required (for now)
- All file types allowed
- Download logging required

## Configuration Needed
- File storage directory path (configurable via appsettings.json)
- Optional: Maximum file size limit
