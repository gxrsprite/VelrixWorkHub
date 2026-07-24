using VelrixWorkHub.Application.Notifications;

namespace VelrixWorkHub.Web.Notifications;

/// <summary>独立消费站外通知 Outbox；未配置渠道时保持 Pending，后续配置 Provider 后再投递。</summary>
public sealed class ExternalNotificationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ExternalNotificationOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DeliverAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await DeliverAsync(stoppingToken);
    }

    private async Task DeliverAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<ExternalNotificationOutboxService>();
            var result = await outbox.DeliverPendingAsync(cancellationToken: stoppingToken);
            logger.LogInformation("站外通知 Outbox 扫描完成：候选 {Candidate}，投递 {Delivered}，失败 {Failed}，跳过 {Skipped}。", result.CandidateCount, result.DeliveredCount, result.FailedCount, result.SkippedCount);
            var highRetries = outbox.InspectChannels().Where(item => item.MaxRetryCount >= 3).ToArray();
            if (highRetries.Length > 0)
                logger.LogWarning("站外通知 Outbox 存在持续失败渠道：{Channels}。", string.Join("，", highRetries.Select(item => $"{item.Channel}（待投递 {item.PendingCount}，最高重试 {item.MaxRetryCount}）")));
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "站外通知 Outbox 扫描失败，将在下一轮重试。");
        }
    }
}
