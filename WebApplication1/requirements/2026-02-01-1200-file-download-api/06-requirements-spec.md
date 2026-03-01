# Requirements Specification: File Download API

## Problem Statement
Users need an API endpoint to download files from the server. The solution should support local filesystem storage initially, with logging for audit purposes.

## Solution Overview
Implement a minimal API endpoint that serves files from a configurable directory on the local filesystem, with structured logging for download tracking.

---

## Functional Requirements

### FR1: File Download Endpoint
- **Endpoint:** `GET /files/{filename}`
- **Behavior:** Return the requested file as a download
- **Response:** File stream with appropriate Content-Type header
- **Example:** `GET /files/report.pdf` returns the PDF file

### FR2: File Not Found Handling
- **Condition:** When requested file does not exist
- **Response:** HTTP 404 with JSON body
- **Format:** `{ "error": "File not found" }`

### FR3: Download Logging
- **Log Level:** Information
- **Log Data:** Timestamp, filename, file size, client IP address
- **Output:** Standard structured logging (console/stdout)

### FR4: Storage Directory Auto-Creation
- **Behavior:** Create the file storage directory on application startup if it doesn't exist
- **Default Path:** Configurable via `appsettings.json`

---

## Technical Requirements

### TR1: File to Modify
- `Program.cs` - Add file download endpoint and configuration

### TR2: Configuration
Add to `appsettings.json`:
```json
{
  "FileStorage": {
    "Path": "./Files"
  }
}
```

### TR3: Implementation Pattern
Follow existing minimal API patterns in `Program.cs`:
- Use `app.MapGet()` for endpoint definition
- Use `TypedResults` for response types
- Use `.WithName()` for OpenAPI documentation

### TR4: Security
- Validate filename to prevent directory traversal attacks (reject `..`, `/`, `\`)
- Sanitize input before constructing file paths

### TR5: Response Headers
- Set `Content-Type` based on file extension (use `application/octet-stream` as fallback)
- Set `Content-Disposition: attachment; filename="{filename}"` to trigger download

---

## Acceptance Criteria

- [ ] `GET /files/{filename}` returns the file when it exists
- [ ] `GET /files/{filename}` returns 404 JSON error when file doesn't exist
- [ ] Directory traversal attempts (e.g., `../etc/passwd`) are rejected
- [ ] Downloads are logged with timestamp, filename, size, and client IP
- [ ] File storage path is configurable via `appsettings.json`
- [ ] Storage directory is created automatically if missing
- [ ] Endpoint appears in OpenAPI documentation

---

## Future Considerations (Out of Scope)
- Authentication/authorization for downloads
- File upload functionality
- Cloud storage backend (S3, Azure Blob)
- Rate limiting
- Resume/range requests for large files

---

## Assumptions
- Files are pre-placed in the storage directory by administrators
- All file types are allowed for download
- Single-instance deployment (no distributed file storage concerns)
