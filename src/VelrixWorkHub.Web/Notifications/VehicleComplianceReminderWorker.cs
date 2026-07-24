using VelrixWorkHub.Application.Vehicles;

namespace VelrixWorkHub.Web.Notifications;

/// <summary>周期扫描车辆年检、保险风险；通知去重键确保重复扫描不会重复投递。</summary>
public sealed class VehicleComplianceReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<VehicleComplianceReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

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
            var result = scope.ServiceProvider.GetRequiredService<VehicleComplianceReminderService>().Scan(DateOnly.FromDateTime(DateTime.Today));
            logger.LogInformation("车辆合规提醒扫描完成：年检到期 {Inspection}，保险到期 {Insurance}，投递尝试 {Attempts}，跳过 {Skipped}。", result.InspectionDueCount, result.InsuranceDueCount, result.NotificationAttemptCount, result.SkippedVehicleCount);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "车辆合规提醒扫描失败，将在下一轮重试。");
        }
        return Task.CompletedTask;
    }
}
