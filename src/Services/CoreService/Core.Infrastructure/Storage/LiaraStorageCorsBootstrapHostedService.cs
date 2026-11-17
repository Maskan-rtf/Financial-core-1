using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Storage;

/// <summary>
/// Browser uploads use S3 presigned PUT against Liara Object Storage.
/// Without bucket CORS, the panel shows a generic network error on XHR.
/// </summary>
public sealed class LiaraStorageCorsBootstrapHostedService(
    ILiaraObjectStorage objectStorage,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<LiaraStorageCorsBootstrapHostedService> logger) : IHostedService
{
    private static readonly string[] DefaultOrigins =
    [
        "https://panel.maskan-rtf.ir",
        "https://maskan-panel.liara.run",
        "https://maskan-rtf.ir",
        "https://www.maskan-rtf.ir",
        "http://localhost:5173",
        "http://localhost:3000",
        "http://127.0.0.1:5173"
    ];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => _ = EnsureCorsAsync());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureCorsAsync()
    {
        try
        {
            var configured = configuration.GetSection("LiaraStorage:CorsAllowedOrigins").Get<string[]>() ?? [];
            var origins = configured
                .Concat(DefaultOrigins)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await objectStorage.EnsureBrowserCorsAsync(origins, CancellationToken.None);
            logger.LogInformation(
                "Ensured Liara Object Storage CORS for {OriginCount} browser origins",
                origins.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure Liara Object Storage CORS; browser uploads may fail until CORS is set on the bucket");
        }
    }
}
