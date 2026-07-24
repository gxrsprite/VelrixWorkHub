namespace AdminBlazor.Services;

/// <summary>
/// 定时任务接口 — 实现此接口并通过 CronSchedulerExtensions 注册
/// </summary>
public interface IScheduledTask
{
    /// <summary>Cron 表达式（5段：分 时 日 月 星期）</summary>
    string Cron { get; }
    /// <summary>任务名称（用于日志）</summary>
    string Name { get; }
    /// <summary>是否启用</summary>
    bool Enabled { get; }
    /// <summary>执行任务</summary>
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
