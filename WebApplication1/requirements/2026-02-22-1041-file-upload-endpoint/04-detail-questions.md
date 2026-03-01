# Detail Questions

## Q6: Should tus upload endpoints be added to a new `UploadEndpoints.cs` file (rather than the existing `FileEndpoints.cs`)?
**Default if unknown:** Yes — follows the existing convention of one file per concern; `FileEndpoints.cs` handles downloads only, a new `UploadEndpoints.cs` is cleaner.

## Q7: Should upload session state (the mapping of upload ID → offset, filename, length) be stored in-memory only, accepting that incomplete uploads are lost on server restart?
**Default if unknown:** Yes — in-memory is simpler, consistent with how `FileService` is registered as a Singleton; durability can be added later if needed.

## Q8: Should the server support the tus `termination` extension — i.e., allow clients to send `DELETE /files/upload/{id}` to explicitly cancel an incomplete upload and clean up the temp file?
**Default if unknown:** Yes — it's a standard tus extension that prevents orphaned temp files and is expected by most tus client libraries.

## Q9: Should the server support the tus `expiration` extension — i.e., automatically expire and clean up incomplete upload sessions after a configurable timeout (e.g., 24 hours)?
**Default if unknown:** Yes — without expiration, abandoned uploads accumulate temp files on disk; a configurable `FileStorage:UploadExpiryHours` setting is the safe default.

## Q10: Should a new `UploadService` be created as a separate service class (in `Services/UploadService.cs` + `IUploadService.cs`), rather than adding upload methods to the existing `FileService`?
**Default if unknown:** Yes — upload concerns (session tracking, temp file management, chunk assembly) are distinct from download concerns and should not mix into `FileService`.
