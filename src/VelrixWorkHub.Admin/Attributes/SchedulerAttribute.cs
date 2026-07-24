namespace AdminBlazor;

/// <summary>
/// 定时任务标记 — 配合调度器发现可调度的方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SchedulerAttribute : Attribute
{
    public string? Cron { get; set; }
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;

    public SchedulerAttribute() { }
    public SchedulerAttribute(string cron) { Cron = cron; }
}
