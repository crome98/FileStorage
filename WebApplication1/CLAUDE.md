# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Run with specific launch profile
dotnet run --launch-profile http
dotnet run --launch-profile https

# Watch mode (auto-rebuild on changes)
dotnet watch run
```

## Project Overview

This is an ASP.NET Core Web API project targeting .NET 10.0 using the minimal API pattern.

**Key characteristics:**
- Minimal API style (no controllers) - endpoints defined directly in `Program.cs`
- OpenAPI/Swagger support enabled via `Microsoft.AspNetCore.OpenApi` package
- OpenAPI endpoint available at `/openapi/v1.json` in Development environment
- Nullable reference types and implicit usings enabled

**Default URLs:**
- HTTP: `http://localhost:5155`
- HTTPS: `https://localhost:7095`
