# Discovery Answers

## Q1: Will uploaded files need to be accessible immediately via the existing download endpoint?
**Answer:** Yes
Files saved to the same `./Files` directory so the existing `GET /files/{filename}` endpoint serves them immediately.

## Q2: Is this API consumed by a client you control?
**Answer:** No — follow tus.io
The API should implement the **tus.io resumable upload protocol** so any standard tus client library can work with it out of the box.

## Q3: Will uploaded files potentially be large (e.g. over 100 MB)?
**Answer:** Yes
Large files are expected. Chunked upload is essential for reliability and to avoid loading entire files into server memory.

## Q4: Should the server enforce a maximum allowed file size for uploads?
**Answer:** Yes
A configurable max file size should be enforced (via `appsettings.json`), similar to how `FileStorage:Path` is already configured.

## Q5: What should happen when a file with the same name is uploaded again?
**Answer:** Overwrite silently, but configurable
Default behavior: new upload replaces the existing file (S3-like semantics).
A configuration flag in `appsettings.json` should allow switching to reject-on-conflict behavior.
