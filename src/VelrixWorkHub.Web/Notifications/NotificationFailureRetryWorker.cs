using VelrixWorkHub.Application.Notifications;

namespace VelrixWorkHub.Web.Notifications;

/// <summary>
/// 定期消费通知失败记录。重试本身由 NotificationFailureRetryService 保证幂等，
/// Worker 只负责宿主调度和失败隔离。
/// </summary>
public sealed class NotificationFailureRetryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationFailureRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetryAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RetryAsync(stoppingToken);
    }

    private Task RetryAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NotificationFailureRetryService>();
            var resolved = service.RetryPending(attemptedAt: DateTime.Now);
            logger.LogInformation("通知失败重试完成：本轮解决 {Resolved} 条。", resolved);
            var summary = service.InspectPending();
            if (summary.HighRetryCount > 0)
                logger.LogWarning("通知失败记录存在持续失败：待处理 {PendingCount} 条，高重试 {HighRetryCount} 条，最高重试 {MaxRetryCount} 次。", summary.PendingCount, summary.HighRetryCount, summary.MaxRetryCount);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "通知失败重试执行失败，将在下一轮重试。");
        }
        return Task.CompletedTask;
    }
}
