using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Tests;

public class FilesEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDirectory;

    public FilesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Path"] = _testDirectory
                });
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task Post_CreateUpload_Returns201WithLocation()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        request.Headers.Add("Tus-Resumable", "1.0.0");
        request.Headers.Add("Upload-Length", "1024");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/files/upload/", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_CreateUpload_WithWrongTusVersion_Returns412()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        request.Headers.Add("Tus-Resumable", "0.9.0");
        request.Headers.Add("Upload-Length", "1024");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task Patch_AppendChunk_Returns204WithUpdatedOffset()
    {
        // First create an upload session
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", "1024");
        var createResponse = await _client.SendAsync(createRequest);
        var uploadUrl = createResponse.Headers.Location!.ToString();

        // Then send a chunk
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
        patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
        patchRequest.Headers.Add("Upload-Offset", "0");
        patchRequest.Content = new ByteArrayContent(new byte[512]);
        patchRequest.Content.Headers.ContentType = new("application/offset+octet-stream");

        var response = await _client.SendAsync(patchRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("512", response.Headers.GetValues("Upload-Offset").First());
    }

    [Fact]
    public async Task Head_GetProgress_Returns200WithOffset()
    {
        // Create an upload session
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", "1024");
        var createResponse = await _client.SendAsync(createRequest);
        var uploadUrl = createResponse.Headers.Location!.ToString();

        // Check progress
        var headRequest = new HttpRequestMessage(HttpMethod.Head, uploadUrl);
        headRequest.Headers.Add("Tus-Resumable", "1.0.0");

        var response = await _client.SendAsync(headRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("0", response.Headers.GetValues("Upload-Offset").First());
        Assert.Equal("1024", response.Headers.GetValues("Upload-Length").First());
        Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").First());
    }

    [Fact]
    public async Task Delete_TerminateUpload_Returns204()
    {
        // Create an upload session
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", "1024");
        var createResponse = await _client.SendAsync(createRequest);
        var uploadUrl = createResponse.Headers.Location!.ToString();

        // Cancel it
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, uploadUrl);
        deleteRequest.Headers.Add("Tus-Resumable", "1.0.0");

        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Patch_CompleteUpload_FileIsDownloadable()
    {
        var content = "hello from tus upload"u8.ToArray();

        // Create session with filename
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", content.Length.ToString());
        createRequest.Headers.Add("Upload-Metadata", "filename dGVzdC50eHQ="); // "test.txt" in base64
        var createResponse = await _client.SendAsync(createRequest);
        var uploadUrl = createResponse.Headers.Location!.ToString();

        // Send final chunk
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
        patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
        patchRequest.Headers.Add("Upload-Offset", "0");
        patchRequest.Content = new ByteArrayContent(content);
        patchRequest.Content.Headers.ContentType = new("application/offset+octet-stream");
        await _client.SendAsync(patchRequest);

        // File should now be downloadable
        var downloadResponse = await _client.GetAsync("/files/test.txt");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var body = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("hello from tus upload", body);
    }

    [Fact]
    public async Task Options_GetCapabilities_ReturnsCorrectHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/files/upload");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("1.0.0", response.Headers.GetValues("Tus-Version").First());
        Assert.Contains("creation", response.Headers.GetValues("Tus-Extension").First());
        Assert.Contains("termination", response.Headers.GetValues("Tus-Extension").First());
        Assert.Contains("expiration", response.Headers.GetValues("Tus-Extension").First());
        Assert.True(response.Headers.Contains("Tus-Max-Size"));
    }

    [Fact]
    public async Task Patch_MultipleChunks_FileIsDownloadable()
    {
        var chunk1 = "hello "u8.ToArray();  // 6 bytes
        var chunk2 = "from "u8.ToArray();   // 5 bytes
        var chunk3 = "tus"u8.ToArray();     // 3 bytes
        var totalSize = chunk1.Length + chunk2.Length + chunk3.Length; // 14 bytes

        // Create session
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/files/upload");
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", totalSize.ToString());
        createRequest.Headers.Add("Upload-Metadata", "filename bXVsdGkudHh0"); // "multi.txt"
        var createResponse = await _client.SendAsync(createRequest);
        var uploadUrl = createResponse.Headers.Location!.ToString();

        // Send 3 chunks sequentially
        foreach (var (chunk, offset) in new[] { (chunk1, 0), (chunk2, 6), (chunk3, 11) })
        {
            var patch = new HttpRequestMessage(HttpMethod.Patch, uploadUrl);
            patch.Headers.Add("Tus-Resumable", "1.0.0");
            patch.Headers.Add("Upload-Offset", offset.ToString());
            patch.Content = new ByteArrayContent(chunk);
            patch.Content.Headers.ContentType = new("application/offset+octet-stream");
            await _client.SendAsync(patch);
        }

        // File should be assembled correctly from all chunks
        var downloadResponse = await _client.GetAsync("/files/multi.txt");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var body = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("hello from tus", body);
    }
}
