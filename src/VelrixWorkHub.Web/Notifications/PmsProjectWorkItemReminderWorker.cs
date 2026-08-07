using VelrixWorkHub.Application.PmsProjects;

namespace VelrixWorkHub.Web.Notifications;

/// <summary>定期扫描项目工作项提醒；通知的稳定去重键使重复执行安全。</summary>
public sealed class PmsProjectWorkItemReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PmsProjectWorkItemReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ScanAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await ScanAsync(stoppingToken);
    }

    private Task ScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var result = scope.ServiceProvider.GetRequiredService<PmsProjectWorkItemReminderService>().Scan(DateTime.Now);
            logger.LogInformation("PMS 工作项提醒扫描完成：人工到期 {Due}，计划逾期 {Overdue}，投递尝试 {Attempts}，跳过 {Skipped}。", result.DueWorkItemCount, result.OverdueWorkItemCount, result.NotificationAttemptCount, result.SkippedWorkItemCount);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "PMS 工作项提醒扫描失败，将在下一轮重试。");
        }
        return Task.CompletedTask;
    }
}
