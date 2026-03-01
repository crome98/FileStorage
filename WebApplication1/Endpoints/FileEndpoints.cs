using WebApplication1;
using WebApplication1.Services;

namespace WebApplication1.Endpoints;

public static class FileEndpoints
{
    public static WebApplication MapFileEndpoints(this WebApplication app)
    {
        app.MapGet("/files/{filename}", DownloadFile)
            .WithName("DownloadFile")
            .Produces(200)
            .Produces<object>(404)
            .Produces<object>(400);

        app.MapMethods("/files/upload", ["OPTIONS"], GetCapabilities)
            .WithName("GetCapabilities")
            .Produces(204)
            .RequireRateLimiting(TusConstants.RateLimitPolicy);

        app.MapPost("/files/upload", CreateUpload)
            .WithName("CreateUpload")
            .Produces(201)
            .Produces<object>(400)
            .Produces<object>(StatusCodes.Status412PreconditionFailed)
            .Produces<object>(StatusCodes.Status413RequestEntityTooLarge)
            .RequireRateLimiting(TusConstants.RateLimitPolicy);

        app.MapPatch("/files/upload/{uploadId}", AppendChunk)
            .WithName("AppendChunk")
            .Produces(204)
            .Produces<object>(400)
            .Produces<object>(404)
            .Produces<object>(409)
            .Produces<object>(StatusCodes.Status415UnsupportedMediaType)
            .RequireRateLimiting(TusConstants.RateLimitPolicy);

        app.MapMethods("/files/upload/{uploadId}", ["HEAD"], GetProgress)
            .WithName("GetProgress")
            .Produces(200)
            .Produces<object>(404)
            .RequireRateLimiting(TusConstants.RateLimitPolicy);

        app.MapDelete("/files/upload/{uploadId}", TerminateUpload)
            .WithName("TerminateUpload")
            .Produces(204)
            .Produces<object>(404)
            .RequireRateLimiting(TusConstants.RateLimitPolicy);

        return app;
    }

    private static IResult GetCapabilities(HttpContext context, IUploadService uploadService)
    {
        context.Response.Headers.Append("Tus-Resumable", TusConstants.Version);
        context.Response.Headers.Append("Tus-Version", TusConstants.Version);
        context.Response.Headers.Append("Tus-Extension", TusConstants.Extensions);
        context.Response.Headers.Append("Tus-Max-Size", uploadService.GetMaxUploadSize().ToString());
        return Results.NoContent();
    }

    private static IResult TerminateUpload(string uploadId, HttpContext context, IUploadService uploadService)
    {
        if (!context.Request.Headers.TryGetValue("Tus-Resumable", out var tusVersion) || tusVersion != TusConstants.Version)
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

        var success = uploadService.Terminate(uploadId);
        if (!success)
            return Results.NotFound();

        context.Response.Headers.Append("Tus-Resumable", TusConstants.Version);
        return Results.NoContent();
    }

    private static IResult GetProgress(string uploadId, HttpContext context, IUploadService uploadService)
    {
        if (!context.Request.Headers.TryGetValue("Tus-Resumable", out var tusVersion) || tusVersion != TusConstants.Version)
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

        var progress = uploadService.GetProgress(uploadId);
        if (progress is null)
            return Results.NotFound();

        context.Response.Headers.Append("Tus-Resumable", TusConstants.Version);
        context.Response.Headers.Append("Upload-Offset", progress.Offset.ToString());
        context.Response.Headers.Append("Upload-Length", progress.Length.ToString());
        context.Response.Headers.Append("Cache-Control", "no-store");
        return Results.Ok();
    }

    private static async Task<IResult> AppendChunk(
        string uploadId, HttpContext context, IUploadService uploadService)
    {
        if (!context.Request.Headers.TryGetValue("Tus-Resumable", out var tusVersion) || tusVersion != TusConstants.Version)
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

        if (!context.Request.Headers.TryGetValue("Upload-Offset", out var offsetHeader)
            || !long.TryParse(offsetHeader, out var clientOffset))
            return Results.BadRequest(new { error = "Missing or invalid Upload-Offset header" });

        if (context.Request.ContentType != TusConstants.OffsetContentType)
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var result = await uploadService.AppendChunk(uploadId, clientOffset, context.Request.Body);

        if (!result.Success)
            return result.Error == "Upload not found"
                ? Results.NotFound(new { error = result.Error })
                : Results.Conflict(new { error = result.Error });

        context.Response.Headers.Append("Tus-Resumable", TusConstants.Version);
        context.Response.Headers.Append("Upload-Offset", result.NewOffset!.Value.ToString());
        return Results.NoContent();
    }

    private static IResult CreateUpload(HttpContext context, IUploadService uploadService)
    {
        if (!context.Request.Headers.TryGetValue("Tus-Resumable", out var tusVersion) || tusVersion != TusConstants.Version)
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

        if (!context.Request.Headers.TryGetValue("Upload-Length", out var uploadLengthHeader)
            || !long.TryParse(uploadLengthHeader, out var uploadLength))
            return Results.BadRequest(new { error = "Missing or invalid Upload-Length header" });

        var filename = GetFilenameFromMetadata(context.Request.Headers["Upload-Metadata"].ToString());
        var result = uploadService.CreateUpload(uploadLength, filename, initialData: null);

        if (!result.Success)
            return result.Error!.Contains("size")
                ? Results.StatusCode(StatusCodes.Status413RequestEntityTooLarge)
                : Results.BadRequest(new { error = result.Error });

        context.Response.Headers.Append("Tus-Resumable", TusConstants.Version);
        return Results.Created($"/files/upload/{result.UploadId}", null);
    }

    private static string? GetFilenameFromMetadata(string metadata)
    {
        if (string.IsNullOrEmpty(metadata)) return null;
        foreach (var pair in metadata.Split(','))
        {
            var parts = pair.Trim().Split(' ');
            if (parts.Length == 2 && parts[0] == "filename")
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
        }
        return null;
    }

    private static IResult DownloadFile(
        string filename,
        HttpContext context,
        IFileService fileService,
        ILogger<FileService> logger)
    {
        var result = fileService.GetFile(filename);

        if (!result.Success)
        {
            return result.Error == "File not found"
                ? Results.NotFound(new { error = result.Error })
                : Results.BadRequest(new { error = result.Error });
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logger.LogInformation(
            "File download: {Filename}, Size: {Size} bytes, ClientIP: {ClientIP}",
            filename, result.FileSize, clientIp);

        return Results.File(result.FilePath!, result.ContentType!, filename);
    }
}
