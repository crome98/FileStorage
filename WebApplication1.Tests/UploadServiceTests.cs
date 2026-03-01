using Microsoft.Extensions.Configuration;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public class UploadServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly UploadService _sut;

    public UploadServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:Path"] = _testDirectory,
                ["FileStorage:MaxUploadSize"] = "104857600",
                ["FileStorage:AllowOverwrite"] = "true",
                ["FileStorage:UploadExpiryHours"] = "24"
            })
            .Build();

        _sut = new UploadService(configuration, new FileService(configuration));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Fact]
    public void CreateUpload_WithValidLength_ReturnsUploadId()
    {
        var result = _sut.CreateUpload(uploadLength: 1024, filename: "test.txt", initialData: null);

        Assert.True(result.Success);
        Assert.NotNull(result.UploadId);
        Assert.NotEmpty(result.UploadId);
    }

    [Fact]
    public void CreateUpload_ExceedingMaxSize_ReturnsFailure()
    {
        var result = _sut.CreateUpload(uploadLength: 200 * 1024 * 1024, filename: "big.zip", initialData: null);
        // MaxUploadSize is set to 104857600 (100 MB) in test config

        Assert.False(result.Success);
        Assert.Equal("Upload length exceeds maximum allowed size", result.Error);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    public void CreateUpload_WithInvalidFilename_ReturnsFailure(string filename)
    {
        var result = _sut.CreateUpload(uploadLength: 1024, filename: filename, initialData: null);

        Assert.False(result.Success);
        Assert.Equal("Invalid filename", result.Error);
    }

    [Fact]
    public void CreateUpload_CreatesSessionWithCorrectInitialOffset()
    {
        var result = _sut.CreateUpload(uploadLength: 1024, filename: "test.txt", initialData: null);

        var progress = _sut.GetProgress(result.UploadId!);

        Assert.NotNull(progress);
        Assert.Equal(0, progress.Offset);
        Assert.Equal(1024, progress.Length);
        Assert.Equal("test.txt", progress.Filename);
    }

    [Fact]
    public void GetProgress_WithUnknownUploadId_ReturnsNull()
    {
        var progress = _sut.GetProgress("nonexistent");

        Assert.Null(progress);
    }

    [Fact]
    public async Task AppendChunk_WritesDataAndUpdatesOffset()
    {
        var created = _sut.CreateUpload(uploadLength: 1024, filename: "test.txt", initialData: null);
        var chunkData = new byte[512];
        using var stream = new MemoryStream(chunkData);

        var result = await _sut.AppendChunk(created.UploadId!, clientOffset: 0, data: stream);

        Assert.True(result.Success);
        Assert.Equal(512, result.NewOffset);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public async Task AppendChunk_WhenOffsetMismatch_ReturnsFailure()
    {
        var created = _sut.CreateUpload(uploadLength: 1024, filename: "test.txt", initialData: null);
        using var stream = new MemoryStream(new byte[512]);

        var result = await _sut.AppendChunk(created.UploadId!, clientOffset: 100, data: stream);

        Assert.False(result.Success);
        Assert.Equal("Offset mismatch", result.Error);
    }

    [Fact]
    public async Task AppendChunk_WhenFinalChunk_IsComplete()
    {
        var created = _sut.CreateUpload(uploadLength: 512, filename: "test.txt", initialData: null);
        using var stream = new MemoryStream(new byte[512]);

        var result = await _sut.AppendChunk(created.UploadId!, clientOffset: 0, data: stream);

        Assert.True(result.Success);
        Assert.Equal(512, result.NewOffset);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task AppendChunk_WhenComplete_MovesFileToStorage()
    {
        var created = _sut.CreateUpload(uploadLength: 512, filename: "final.txt", initialData: null);
        using var stream = new MemoryStream(new byte[512]);

        await _sut.AppendChunk(created.UploadId!, clientOffset: 0, data: stream);

        var finalPath = Path.Combine(_testDirectory, "final.txt");
        Assert.True(File.Exists(finalPath));
        Assert.False(File.Exists(Path.Combine(_testDirectory, ".uploads", $"{created.UploadId}.tmp")));
    }

    [Fact]
    public async Task Terminate_RemovesSessionAndDeletesTempFile()
    {
        var created = _sut.CreateUpload(uploadLength: 1024, filename: "test.txt", initialData: null);
        using var stream = new MemoryStream(new byte[512]);
        await _sut.AppendChunk(created.UploadId!, clientOffset: 0, data: stream);

        var result = _sut.Terminate(created.UploadId!);

        Assert.True(result);
        Assert.Null(_sut.GetProgress(created.UploadId!));
        Assert.False(File.Exists(Path.Combine(_testDirectory, ".uploads", $"{created.UploadId}.tmp")));
    }

    [Fact]
    public void Terminate_WithUnknownUploadId_ReturnsFalse()
    {
        var result = _sut.Terminate("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task PurgeExpiredSessions_RemovesExpiredSessionsAndTempFiles()
    {
        // UploadExpiryHours = 0 means sessions expire immediately
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:Path"] = _testDirectory,
                ["FileStorage:UploadExpiryHours"] = "0"
            })
            .Build();
        var sut = new UploadService(configuration, new FileService(configuration));

        // Create a session and write a partial chunk so temp file exists
        var created = sut.CreateUpload(uploadLength: 1024, filename: "expiring.txt", initialData: null);
        using var stream = new MemoryStream(new byte[512]);
        await sut.AppendChunk(created.UploadId!, clientOffset: 0, data: stream);

        // Purge — session should be expired and cleaned up
        sut.PurgeExpiredSessions();

        Assert.Null(sut.GetProgress(created.UploadId!));
        Assert.False(File.Exists(Path.Combine(_testDirectory, ".uploads", $"{created.UploadId}.tmp")));
    }
}
