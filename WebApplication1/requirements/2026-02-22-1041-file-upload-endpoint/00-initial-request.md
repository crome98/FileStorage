# Initial Request

**Date:** 2026-02-22
**Feature:** File Upload Endpoint

## User Request

> I would like to add a file upload endpoint to the backend which allows chunks upload as well as normal upload.

## Summary

Add a POST endpoint to the existing ASP.NET Core Web API that supports:
1. Normal (single-request) file upload
2. Chunked file upload (uploading a file in multiple parts)

The existing backend already has a file download endpoint (`GET /files/{filename}`) backed by `FileService` storing files in a configurable `./Files` directory.
