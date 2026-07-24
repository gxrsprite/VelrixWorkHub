using VelrixWorkHub.Application.SimpleForms;

namespace VelrixWorkHub.Web.Notifications;

public sealed class SimpleFormCompletionOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<SimpleFormCompletionOutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DispatchAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await DispatchAsync(stoppingToken);
    }
    private Task DispatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var delivered = scope.ServiceProvider.GetRequiredService<SimpleFormCompletionOutboxService>().DispatchPending();
            logger.LogInformation("简单表单完成事件 Outbox 投递完成：{Delivered} 条。", delivered);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "简单表单完成事件 Outbox 投递失败，将在下一轮重试。"); }
        return Task.CompletedTask;
    }
}
