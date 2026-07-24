using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdminBlazor.Services;

/// <summary>
/// 从数据库加载人工维护的节假日覆盖项。
/// </summary>
public class SysHolidayCalendarLoader : IHostedService
{
    private readonly FreeSqlCloud<string> _fsqlCloud;
    private readonly WorkingDayCalendar _calendar;
    private readonly ILogger<SysHolidayCalendarLoader> _logger;

    public SysHolidayCalendarLoader(
        FreeSqlCloud<string> fsqlCloud,
        WorkingDayCalendar calendar,
        ILogger<SysHolidayCalendarLoader> logger)
    {
        _fsqlCloud = fsqlCloud;
        _calendar = calendar;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var fsql = _fsqlCloud.Use("main");
        var rows = await fsql.Select<SysHoliday>()
            .Where(a => a.Enabled)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
            _calendar.ApplyOverride(row.Date, row.Type == SysHolidayType.Workday);

        _logger.LogInformation("SysHoliday: Loaded {Count} calendar overrides from database", rows.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
