# Discovery Questions

## Q1: Will uploaded files need to be accessible immediately via the existing download endpoint (`GET /files/{filename}`)?
**Default if unknown:** Yes (the download endpoint already stores files in `./Files`, so upload should save there too, making the full upload/download cycle work end-to-end)

## Q2: Is this API consumed by a client you control (e.g. a frontend app or internal tool), rather than by arbitrary third-party clients?
**Default if unknown:** Yes (most internal projects upload from a known client, which influences how the chunked upload protocol is designed)

## Q3: Will uploaded files potentially be large (e.g. over 100 MB)?
**Default if unknown:** Yes (chunked upload is typically only needed for large files; if yes, this confirms chunking is needed for reliability/memory concerns)

## Q4: Should the server enforce a maximum allowed file size for uploads?
**Default if unknown:** Yes (no size limit is a security risk — defaulting to enforcing a configurable max is the safer choice)

## Q5: Should uploaded files overwrite an existing file if the same filename is uploaded again?
**Default if unknown:** No (preserving existing files by default is safer; a conflict should return an error or require explicit overwrite)
