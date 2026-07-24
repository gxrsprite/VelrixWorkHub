namespace AdminBlazor.Services;

/// <summary>向管理 UI 和宿主 API 暴露的调度器只读查询契约。</summary>
public interface ICronScheduler
{
    IReadOnlyList<CronTaskViewModel> GetTasks();
}

public sealed class CronTaskViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public DateTime NextFireTime { get; set; }
    public bool Enabled { get; set; }
    public bool SkipHolidays { get; set; }
}
