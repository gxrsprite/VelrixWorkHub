using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdminBlazor.Services;

public static class CronSchedulerExtensions
{
    public static IServiceCollection AddCronScheduler(this IServiceCollection services, Action<CronSchedulerOptions> configure)
    {
        var options = new CronSchedulerOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddSingleton<CronSchedulerService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<CronSchedulerService>());
        return services;
    }

    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services) where T : class, IScheduledTask
    {
        services.AddTransient<T>();
        services.AddSingleton(new ScheduledTaskRegistration(typeof(T)));
        return services;
    }
}

public class CronSchedulerOptions
{
    internal List<CronTaskConfig> Tasks { get; } = new();

    /// <summary>添加一个 Lambda 定时任务</summary>
    public CronSchedulerOptions AddTask(string name, string cron,
        Func<IServiceProvider, CancellationToken, Task> action,
        bool enabled = true, bool skipHolidays = false)
    {
        Tasks.Add(new CronTaskConfig(name, cron, action, enabled, skipHolidays));
        return this;
    }

    /// <summary>添加一个 Lambda 定时任务（无 CancellationToken 重载）</summary>
    public CronSchedulerOptions AddTask(string name, string cron,
        Func<IServiceProvider, Task> action,
        bool enabled = true, bool skipHolidays = false)
    {
        Tasks.Add(new CronTaskConfig(name, cron, (sp, ct) => action(sp), enabled, skipHolidays));
        return this;
    }

    /// <summary>添加一个 IScheduledTask 实例</summary>
    public CronSchedulerOptions AddTask(IScheduledTask task, bool skipHolidays = false)
    {
        Tasks.Add(new CronTaskConfig(task.Name, task.Cron,
            (sp, ct) => task.ExecuteAsync(sp, ct), task.Enabled, skipHolidays));
        return this;
    }
}

public record CronTaskConfig(
    string Name,
    string Cron,
    Func<IServiceProvider, CancellationToken, Task> Action,
    bool Enabled,
    bool SkipHolidays
);

public record ScheduledTaskRegistration(Type TaskType);
