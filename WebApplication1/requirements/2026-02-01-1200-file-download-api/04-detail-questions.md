# Expert Detail Questions

## Q1: Should files be identified by filename in the URL (e.g., `/files/document.pdf`)?
**Default if unknown:** Yes (most intuitive API design for file downloads)

## Q2: Should the API include an endpoint to list available files?
**Default if unknown:** Yes (helps users discover what files are available)

## Q3: Should the download logs be written to a file (vs. just console output)?
**Default if unknown:** No (console/structured logging is sufficient initially)

## Q4: Should the file storage directory be created automatically if it doesn't exist?
**Default if unknown:** Yes (better developer experience)

## Q5: Should the API return a 404 error with a JSON message when a file is not found?
**Default if unknown:** Yes (consistent API error responses)
