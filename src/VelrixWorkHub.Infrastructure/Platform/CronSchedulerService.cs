using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace AdminBlazor.Services;

public class CronSchedulerService : BackgroundService, ICronScheduler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CronSchedulerService> _logger;
    private readonly CronSchedulerOptions? _options;
    private readonly IEnumerable<ScheduledTaskRegistration> _taskRegistrations;
    private readonly WorkingDayCalendar? _workingDayCalendar;
    private readonly List<CronTaskEntry> _tasks = new();
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(1);

    public CronSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<CronSchedulerService> logger,
        CronSchedulerOptions? options = null,
        IEnumerable<ScheduledTaskRegistration>? taskRegistrations = null,
        WorkingDayCalendar? workingDayCalendar = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
        _taskRegistrations = taskRegistrations ?? Array.Empty<ScheduledTaskRegistration>();
        _workingDayCalendar = workingDayCalendar;
    }

    void AddTask(CronTaskConfig config)
    {
        var schedule = CrontabSchedule.Parse(config.Cron, new CrontabSchedule.ParseOptions { IncludingSeconds = false });
        _tasks.Add(new CronTaskEntry
        {
            Name = config.Name,
            Cron = config.Cron,
            Schedule = schedule,
            Action = config.Action,
            Enabled = config.Enabled,
            SkipHolidays = config.SkipHolidays,
            NextFireTime = schedule.GetNextOccurrence(DateTime.Now)
        });
        var holidayNote = config.SkipHolidays ? " [跳过节假日]" : "";
        _logger.LogInformation("CronScheduler: '{Name}' cron='{Cron}' next={Next:yyyy-MM-dd HH:mm:ss}{HolidayNote}",
            config.Name, config.Cron, _tasks[^1].NextFireTime, holidayNote);
    }

    public IReadOnlyList<CronTaskViewModel> GetTasks()
    {
        return _tasks.Select(t => new CronTaskViewModel
        {
            Name = t.Name,
            Cron = t.Cron,
            NextFireTime = t.NextFireTime,
            Enabled = t.Enabled,
            SkipHolidays = t.SkipHolidays
        }).ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options != null)
        {
            foreach (var config in _options.Tasks)
                AddTask(config);
        }

        foreach (var reg in _taskRegistrations)
        {
            var task = (IScheduledTask)_serviceProvider.GetRequiredService(reg.TaskType);
            AddTask(new CronTaskConfig(task.Name, task.Cron,
                (sp, ct) => task.ExecuteAsync(sp, ct), task.Enabled, SkipHolidays: false));
        }

        _logger.LogInformation("CronScheduler: Started with {Count} tasks", _tasks.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            foreach (var entry in _tasks)
            {
                if (!entry.Enabled || entry.NextFireTime > now)
                    continue;

                // 跳过假日检查
                if (entry.SkipHolidays && _workingDayCalendar != null)
                {
                    if (!_workingDayCalendar.IsWorkingDay(now))
                    {
                        _logger.LogDebug("CronScheduler: Skipped '{Name}' — today is a holiday", entry.Name);
                        entry.NextFireTime = entry.Schedule.GetNextOccurrence(entry.NextFireTime);
                        continue;
                    }
                }

                _ = ExecuteTaskAsync(entry, stoppingToken);

                while (entry.NextFireTime <= now)
                    entry.NextFireTime = entry.Schedule.GetNextOccurrence(entry.NextFireTime);
            }
            await Task.Delay(_tickInterval, stoppingToken);
        }
    }

    async Task ExecuteTaskAsync(CronTaskEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            _logger.LogDebug("CronScheduler: Executing '{Name}'", entry.Name);
            await entry.Action(scope.ServiceProvider, cancellationToken);
            _logger.LogInformation("CronScheduler: Completed '{Name}'", entry.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CronScheduler: Task '{Name}' failed", entry.Name);
        }
    }

    class CronTaskEntry
    {
        public string Name { get; set; } = "";
        public string Cron { get; set; } = "";
        public CrontabSchedule Schedule { get; set; } = default!;
        public Func<IServiceProvider, CancellationToken, Task> Action { get; set; } = default!;
        public bool Enabled { get; set; }
        public bool SkipHolidays { get; set; }
        public DateTime NextFireTime { get; set; }
    }
}
