# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run the web application
dotnet run --project WebApplication1

# Run with watch mode (auto-rebuild on changes)
dotnet watch run --project WebApplication1

# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~FileServiceTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~FileServiceTests.GetFile_WhenFileExists_ReturnsSuccess"
```

## Project Structure

This is a .NET 10.0 solution with two projects:

- **WebApplication1/** - ASP.NET Core Web API using minimal API pattern
- **WebApplication1.Tests/** - xUnit test project

## Architecture

### Minimal API Pattern
Endpoints are defined as extension methods on `WebApplication` rather than using controllers. The pattern:
- `Endpoints/` folder contains static classes with `Map*Endpoints` extension methods
- `Program.cs` calls these extension methods (e.g., `app.MapFileEndpoints()`)
- Each endpoint file groups related routes together

### Service Layer
- Services are defined in `Services/` with interface + implementation pairs
- Registered as singletons in `Program.cs` via DI container
- Example: `IFileService`/`FileService` for file operations

### Configuration
- File storage path configured via `FileStorage:Path` in appsettings.json (defaults to `./Files`)
- OpenAPI endpoint available at `/openapi/v1.json` in Development environment

### Testing
- Tests use xUnit with the `[Fact]` and `[Theory]` attributes
- Test classes implement `IDisposable` for cleanup when using temp directories
- Tests inject configuration via `ConfigurationBuilder.AddInMemoryCollection()`

## Default URLs
- HTTP: `http://localhost:5155`
- HTTPS: `https://localhost:7095`
