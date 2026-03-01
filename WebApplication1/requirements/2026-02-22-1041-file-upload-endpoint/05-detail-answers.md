# Detail Answers

## Q6: Endpoint file organization
**Answer:** Rename `FileEndpoints.cs` → `FilesEndpoints.cs` (one file per resource).
All file operations (download + upload + future delete) live in `FilesEndpoints.cs`.
This matches Microsoft's minimal API conventions and scales cleanly.

## Q7: Upload session state storage
**Answer:** In-memory only (Singleton dictionary).
Simple, no extra dependencies. Matches the existing `FileService` Singleton pattern.
Can be swapped to Redis later by changing DI registration in `Program.cs`.

## Q8: tus `termination` extension (DELETE to cancel upload)
**Answer:** Yes — support `DELETE /files/upload/{id}`.
Prevents orphaned temp files and is expected by standard tus client libraries.

## Q9: tus `expiration` extension (auto-cleanup of stale sessions)
**Answer:** Yes — auto-expire incomplete sessions after a configurable timeout.
Add `FileStorage:UploadExpiryHours` to `appsettings.json`.
Background cleanup via a hosted service or lazy expiry check on each request.

## Q10 (implicit): Separate UploadService?
**Answer:** Yes — create `Services/IUploadService.cs` + `Services/UploadService.cs`.
Upload concerns (session tracking, temp file management, chunk assembly) are distinct from
download concerns in `FileService`. Separation follows the existing pattern.
