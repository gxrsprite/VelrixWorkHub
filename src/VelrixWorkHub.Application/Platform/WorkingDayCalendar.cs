namespace AdminBlazor.Services;

/// <summary>
/// 工作日历 — 维护法定假日和调休上班日，支持 IsWorkingDay 判断
///
/// 使用方式：
///   builder.Services.AddSingleton(WorkingDayCalendar.China2026());
///   或手动: new WorkingDayCalendar { Holidays = [...], Workdays = [...] }
/// </summary>
public class WorkingDayCalendar
{
    /// <summary>法定假日（含周末连休）</summary>
    public HashSet<DateTime> Holidays { get; set; } = new();

    /// <summary>调休上班的周末（补班日）</summary>
    public HashSet<DateTime> Workdays { get; set; } = new();

    /// <summary>判断指定日期是否为工作日</summary>
    public bool IsWorkingDay(DateTime date)
    {
        var day = date.Date;
        if (Workdays.Contains(day)) return true;   // 调休上班
        if (Holidays.Contains(day)) return false;   // 法定假日
        return day.DayOfWeek != DayOfWeek.Saturday
            && day.DayOfWeek != DayOfWeek.Sunday;   // 默认周一至周五工作
    }

    /// <summary>今天是否为工作日</summary>
    public bool IsTodayWorkingDay => IsWorkingDay(DateTime.Today);

    public void ApplyOverride(DateTime date, bool isWorkday)
    {
        var day = date.Date;
        Holidays.Remove(day);
        Workdays.Remove(day);

        if (isWorkday)
            Workdays.Add(day);
        else
            Holidays.Add(day);
    }

    public void RemoveOverride(DateTime date)
    {
        var day = date.Date;
        Holidays.Remove(day);
        Workdays.Remove(day);
    }

    /// <summary>2026年中国法定节假日（国务院发布）</summary>
    public static WorkingDayCalendar China2026()
    {
        var cal = new WorkingDayCalendar
        {
            Holidays = new HashSet<DateTime>
            {
                // 元旦：1月1-3日
                new(2026, 1, 1), new(2026, 1, 2), new(2026, 1, 3),
                // 春节：2月17-23日（除夕2月16日）
                new(2026, 2, 16), new(2026, 2, 17), new(2026, 2, 18),
                new(2026, 2, 19), new(2026, 2, 20), new(2026, 2, 21),
                new(2026, 2, 22), new(2026, 2, 23),
                // 清明节：4月5-7日
                new(2026, 4, 5), new(2026, 4, 6), new(2026, 4, 7),
                // 劳动节：5月1-5日
                new(2026, 5, 1), new(2026, 5, 2), new(2026, 5, 3),
                new(2026, 5, 4), new(2026, 5, 5),
                // 端午节：6月19-21日
                new(2026, 6, 19), new(2026, 6, 20), new(2026, 6, 21),
                // 中秋节：9月25-27日
                new(2026, 9, 25), new(2026, 9, 26), new(2026, 9, 27),
                // 国庆节：10月1-7日
                new(2026, 10, 1), new(2026, 10, 2), new(2026, 10, 3),
                new(2026, 10, 4), new(2026, 10, 5), new(2026, 10, 6),
                new(2026, 10, 7),
            },
            Workdays = new HashSet<DateTime>
            {
                // 春节调休
                new(2026, 2, 14), // 周六上班
                new(2026, 2, 15), // 周日上班
                // 劳动节调休
                new(2026, 4, 27), // 周日上班(补5月4日周一)
                // 端午节调休
                new(2026, 6, 17), // 周三(补6月22日周一) — 实际安排待确认
                // 中秋节调休
                new(2026, 9, 28), // 周一(补9月27日周日) — 实际安排待确认
                // 国庆节调休
                new(2026, 9, 19), // 周六上班(补10月5日周一)
                new(2026, 10, 10), // 周六上班(补10月6日周二)
                new(2026, 10, 11), // 周日上班(补10月7日周三)
            },
        };
        return cal;
    }
}
