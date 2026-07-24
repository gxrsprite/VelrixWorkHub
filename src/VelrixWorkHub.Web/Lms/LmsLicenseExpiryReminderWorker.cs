using VelrixWorkHub.Application.Lms;

namespace VelrixWorkHub.Web.Lms;

/// <summary>每天扫描一次授权到期；通知本身由稳定去重键保证重复执行安全。</summary>
public sealed class LmsLicenseExpiryReminderWorker(IServiceScopeFactory scopeFactory, ILogger<LmsLicenseExpiryReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

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
            var result = scope.ServiceProvider.GetRequiredService<LmsLicenseExpiryReminderService>().Scan(DateTime.Now);
            logger.LogInformation("LMS 授权到期扫描完成：临期 {Expiring}，已到期 {Expired}，跳过 {Skipped}。", result.ExpiringNotifications, result.ExpiredNotifications, result.SkippedAuthorizations);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "LMS 授权到期扫描失败，将在下一轮重试。");
        }
        return Task.CompletedTask;
    }
}
