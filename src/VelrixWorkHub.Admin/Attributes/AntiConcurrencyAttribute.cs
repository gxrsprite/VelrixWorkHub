using System.Collections.Concurrent;

namespace AdminBlazor;

/// <summary>
/// 防抖标记 — 指定时间内同一个key只允许执行一次
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AntiConcurrencyAttribute : Attribute
{
    public int IntervalSeconds { get; set; } = 3;

    private static readonly ConcurrentDictionary<string, DateTime> _lastExecution = new();

    /// <summary>检查是否允许执行。若在间隔内返回false。</summary>
    public static bool TryEnter(string key, int intervalSeconds = 3)
    {
        if (_lastExecution.TryGetValue(key, out var lastTime)
            && (DateTime.Now - lastTime).TotalSeconds < intervalSeconds)
            return false;

        _lastExecution[key] = DateTime.Now;
        return true;
    }
}
