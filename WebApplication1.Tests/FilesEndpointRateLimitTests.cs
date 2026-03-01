using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Tests;

public class FilesEndpointRateLimitTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _testDirectory;

    public FilesEndpointRateLimitTests(WebApplicationFactory<Program> factory)
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Path"] = _testDirectory,
                    ["RateLimit:PermitLimit"] = "2",
                    ["RateLimit:WindowMinutes"] = "1"
                });
            });
        });

        _client = customFactory.CreateClient();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public async Task UploadEndpoints_WhenRateLimitExceeded_Returns429()
    {
        await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/files/upload"));
        await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/files/upload"));

        // Third request exceeds the limit
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/files/upload"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
