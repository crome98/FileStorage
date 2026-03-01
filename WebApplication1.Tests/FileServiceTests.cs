using Microsoft.Extensions.Configuration;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public class FileServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileService _sut;

    public FileServiceTests()
    {
        // Create temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        // Configure FileService to use test directory
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:Path"] = _testDirectory
            })
            .Build();

        _sut = new FileService(configuration);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("file.txt", true)]
    [InlineData("document.pdf", true)]
    [InlineData("image.jpg", true)]
    [InlineData("../secret.txt", false)]
    [InlineData("..\\secret.txt", false)]
    [InlineData("folder/file.txt", false)]
    [InlineData("folder\\file.txt", false)]
    public void IsValidFilename_ValidatesCorrectly(string filename, bool expected)
    {
        var result = _sut.IsValidFilename(filename);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFile_WhenFileExists_ReturnsSuccess()
    {
        // Arrange
        var filename = "test.txt";
        var filePath = Path.Combine(_testDirectory, filename);
        File.WriteAllText(filePath, "test content");

        // Act
        var result = _sut.GetFile(filename);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(filePath, result.FilePath);
        Assert.Equal("text/plain", result.ContentType);
        Assert.NotNull(result.FileSize);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GetFile_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var result = _sut.GetFile("nonexistent.txt");

        Assert.False(result.Success);
        Assert.Equal("File not found", result.Error);
        Assert.Null(result.FilePath);
    }

    [Fact]
    public void GetFile_WhenPathTraversal_ReturnsInvalidFilename()
    {
        var result = _sut.GetFile("../etc/passwd");

        Assert.False(result.Success);
        Assert.Equal("Invalid filename", result.Error);
    }

    [Theory]
    [InlineData("file.pdf", "application/pdf")]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.jpeg", "image/jpeg")]
    [InlineData("image.png", "image/png")]
    [InlineData("image.gif", "image/gif")]
    [InlineData("doc.txt", "text/plain")]
    [InlineData("page.html", "text/html")]
    [InlineData("data.json", "application/json")]
    [InlineData("data.xml", "application/xml")]
    [InlineData("archive.zip", "application/zip")]
    [InlineData("unknown.xyz", "application/octet-stream")]
    public void GetContentType_ReturnsCorrectMimeType(string filename, string expectedContentType)
    {
        var result = _sut.GetContentType(filename);

        Assert.Equal(expectedContentType, result);
    }
}
