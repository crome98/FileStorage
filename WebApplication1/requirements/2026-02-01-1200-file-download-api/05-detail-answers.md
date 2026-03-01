# Expert Detail Answers

## Q1: Should files be identified by filename in the URL (e.g., `/files/document.pdf`)?
**Answer:** Yes - use filename in URL path (e.g., `GET /files/{filename}`)

## Q2: Should the API include an endpoint to list available files?
**Answer:** No - users must know the filename upfront. Follow big tech patterns (S3, GCS) where you request specific files directly.

## Q3: Should the download logs be written to a file (vs. just console output)?
**Answer:** Use standard structured logging (console/stdout). Big tech uses log aggregators that collect from stdout - this can be configured later.

## Q4: Should the file storage directory be created automatically if it doesn't exist?
**Answer:** Yes - create the directory automatically on startup.

## Q5: Should the API return a JSON error response when a file is not found?
**Answer:** Yes - return JSON with error message for consistency.
