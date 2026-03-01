namespace WebApplication1.Services;

public class UploadExpiryService : BackgroundService
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);

    private readonly IUploadService _uploadService;
    private readonly ILogger<UploadExpiryService> _logger;

    public UploadExpiryService(IUploadService uploadService, ILogger<UploadExpiryService> logger)
    {
        _uploadService = uploadService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _uploadService.PurgeExpiredSessions();
            _logger.LogInformation("Purged expired upload sessions");
            await Task.Delay(PurgeInterval, stoppingToken);
        }
    }
}
