using System;

public static class DateExtension
{
	/// <summary>
	/// 时间戳起始日期
	/// </summary>
	public static readonly DateTime TimestampStart = new DateTime(1970, 1, 1, 0, 0, 0, 0);

	public static string GetTimeAgo(DateTime targetTime)
	{
		TimeSpan timeSpan = DateTime.Now - targetTime;
		if (timeSpan.TotalDays >= 1.0)
		{
			return $"{(int)timeSpan.TotalDays}天前";
		}
		if (timeSpan.TotalHours >= 1.0)
		{
			return $"{(int)timeSpan.TotalHours}小时前";
		}
		if (timeSpan.TotalMinutes >= 1.0)
		{
			return $"{(int)timeSpan.TotalMinutes}分钟前";
		}
		if (timeSpan.TotalSeconds >= 10.0)
		{
			return $"{(int)timeSpan.TotalMinutes}分钟前";
		}
		return "刚刚";
	}

	/// <summary>
	/// 转换为时间戳
	/// </summary>
	/// <param name="dateTime"></param>
	/// <param name="milliseconds">是否使用毫秒</param>
	/// <returns></returns>
	public static long ToTimestamp(this DateTime dateTime, bool milliseconds = false)
	{
		TimeSpan timeSpan = dateTime.ToUniversalTime() - TimestampStart;
		return (long)(milliseconds ? timeSpan.TotalMilliseconds : timeSpan.TotalSeconds);
	}

	/// <summary>
	/// 获取周几
	/// </summary>
	/// <param name="datetime"></param>
	/// <returns></returns>
	public static string GetWeekName(this DateTime datetime)
	{
		int dayOfWeek = (int)datetime.DayOfWeek;
		string[] array = new string[7] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
		return array[dayOfWeek];
	}
}
