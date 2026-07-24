using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdminBlazor.Services;

/// <summary>
/// 从 Chinese Days CDN 加载法定节假日数据，填充 WorkingDayCalendar
/// 数据源：https://github.com/vsme/chinese-days
/// 启动时自动拉取，失败则用本地缓存兜底
/// </summary>
public class ChinaDaysCalendarLoader : IHostedService
{
    private readonly WorkingDayCalendar _calendar;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ChinaDaysCalendarLoader> _logger;
    private readonly HttpClient _http;

    private const string CdnBaseUrl = "https://cdn.jsdelivr.net/npm/chinese-days/dist/years";
    private const string CacheFileName = "holidays-cache.json";

    public ChinaDaysCalendarLoader(
        WorkingDayCalendar calendar,
        IHostEnvironment env,
        ILogger<ChinaDaysCalendarLoader> logger,
        IHttpClientFactory httpClientFactory)
    {
        _calendar = calendar;
        _env = env;
        _logger = logger;
        _http = httpClientFactory.CreateClient("ChinaDays");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var cachePath = Path.Combine(_env.ContentRootPath, CacheFileName);
        var currentYear = DateTime.Now.Year;

        // 拉取当年 + 下一年
        var years = new[] { currentYear, currentYear + 1 };
        var allHolidays = new HashSet<DateTime>();
        var allWorkdays = new HashSet<DateTime>();

        foreach (var year in years)
        {
            try
            {
                var url = $"{CdnBaseUrl}/{year}.json";
                _logger.LogInformation("ChinaDays: Fetching {Url}", url);
                var json = await _http.GetStringAsync(url, cancellationToken);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                foreach (var dateStr in root.GetProperty("holidays").EnumerateObject())
                    if (DateTime.TryParse(dateStr.Name, out var d)) allHolidays.Add(d);

                foreach (var dateStr in root.GetProperty("workdays").EnumerateObject())
                    if (DateTime.TryParse(dateStr.Name, out var d)) allWorkdays.Add(d);

                // inLieuDays are also non-working
                if (root.TryGetProperty("inLieuDays", out var inLieu))
                    foreach (var dateStr in inLieu.EnumerateObject())
                        if (DateTime.TryParse(dateStr.Name, out var d)) allHolidays.Add(d);

                _logger.LogInformation("ChinaDays: Year {Year} loaded — {Holidays} holidays, {Workdays} workdays",
                    year,
                    root.GetProperty("holidays").EnumerateObject().Count(),
                    root.GetProperty("workdays").EnumerateObject().Count());
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("ChinaDays: CDN fetch failed for {Year}: {Msg}. Trying cache...", year, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("ChinaDays: Failed to parse {Year}: {Msg}", year, ex.Message);
            }
        }

        if (allHolidays.Count > 0 || allWorkdays.Count > 0)
        {
            _calendar.Holidays = allHolidays;
            _calendar.Workdays = allWorkdays;
            _logger.LogInformation("ChinaDays: Calendar loaded — {Holidays} holidays, {Workdays} workdays total",
                allHolidays.Count, allWorkdays.Count);

            // 写本地缓存
            try
            {
                var cache = JsonSerializer.Serialize(new HolidayCache
                {
                    Holidays = allHolidays.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
                    Workdays = allWorkdays.Select(d => d.ToString("yyyy-MM-dd")).ToList()
                });
                await File.WriteAllTextAsync(cachePath, cache, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("ChinaDays: Failed to write cache: {Msg}", ex.Message);
            }
        }
        else
        {
            // CDN 完全失败，从本地缓存加载
            _logger.LogWarning("ChinaDays: CDN unavailable, loading from cache");
            LoadFromCache(cachePath);
        }
    }

    void LoadFromCache(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath)) return;
            var json = File.ReadAllText(cachePath);
            var cache = JsonSerializer.Deserialize<HolidayCache>(json);
            if (cache == null) return;

            _calendar.Holidays = cache.Holidays.Select(DateTime.Parse).ToHashSet();
            _calendar.Workdays = cache.Workdays.Select(DateTime.Parse).ToHashSet();
            _logger.LogInformation("ChinaDays: Loaded from cache — {Holidays} holidays, {Workdays} workdays",
                _calendar.Holidays.Count, _calendar.Workdays.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ChinaDays: Cache load failed: {Msg}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    class HolidayCache
    {
        public List<string> Holidays { get; set; } = new();
        public List<string> Workdays { get; set; } = new();
    }
}
