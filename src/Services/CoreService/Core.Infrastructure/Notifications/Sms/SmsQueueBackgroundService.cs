using Core.Application.Logging;
using Core.Application.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Notifications.Sms;

public sealed class SmsQueueBackgroundService(
    SmsDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SmsQueueBackgroundService> logger) : BackgroundService
{
    private readonly List<SmsQueuedMessage> _pending = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = Task.Run(() => PumpQueueAsync(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "SMS queue processing cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        await reader;
    }

    private async Task PumpQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            lock (_pending)
            {
                _pending.Add(message);
            }
        }
    }

    private async Task ProcessDueMessagesAsync(CancellationToken stoppingToken)
    {
        List<SmsQueuedMessage> due;
        lock (_pending)
        {
            var now = DateTimeOffset.UtcNow;
            due = _pending.Where(m => m.NotBeforeUtc <= now).ToList();
            foreach (var item in due)
                _pending.Remove(item);
        }

        if (due.Count == 0)
            return;

        ApplicationLog.Completed(logger,
            "SMS queue processing {Count} message(s)",
            due.Count);

        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ISmsDispatcher>();

        foreach (var item in due)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            if (!string.IsNullOrWhiteSpace(item.RawMessage))
            {
                await dispatcher.SendImmediateRawAsync(item.Mobile, item.RawMessage, item.CaseId, stoppingToken);
                continue;
            }

            if (item.TemplateId is { } templateId)
                await dispatcher.SendImmediateAsync(templateId, item.Mobile, item.Args, stoppingToken);
        }
    }
}
