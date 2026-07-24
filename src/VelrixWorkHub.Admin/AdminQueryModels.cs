using System.Linq;
using FreeSql;

namespace BootstrapBlazor.Components;

public interface IAdminSortTable
{
    string? SortField { get; }
    bool SortAsc { get; }
    Task SortByAsync(string field);
}

/// <summary>
/// 查询状态容器
/// </summary>
public class AdminQueryInfo
{
    public string? SearchText { get; set; }
    public string? Sort { get; set; }
    public AdminFilterInfo[] Filters { get; set; } = Array.Empty<AdminFilterInfo>();
    public long Total { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int MaxPageNumber { get; set; }
    public bool IsQueryString { get; set; }
    public bool IsTracking { get; set; }
    public Func<Task>? InvokeQueryAsync { get; set; }
    public Func<Task>? InvokeAddAsync { get; set; }
}

/// <summary>
/// 查询事件参数
/// </summary>
public record AdminQueryEventArgs<TItem>(ISelect<TItem> Select, string? SearchText, AdminFilterInfo[] Filters, string? Sort);

/// <summary>
/// 确认事件参数
/// </summary>
public class AdminConfirmEventArgs<TItem>
{
    public TItem Argument { get; set; }
    public bool Cancel { get; set; }
    public string? Message { get; set; }

    public AdminConfirmEventArgs(TItem argument)
    {
        Argument = argument;
    }
}

/// <summary>
/// 过滤类型
/// </summary>
public enum AdminFilterType
{
    Tags,
    TagsMultiple,
    DateRange,
    Text
}

/// <summary>
/// 过滤信息
/// </summary>
public class AdminFilterInfo
{
    public string Label { get; set; } = "";
    public string QueryStringName { get; set; } = "";
    public AdminFilterType Type { get; set; }
    public AdminOptions[]? Options { get; set; }
    public int Col { get; set; } = 12;
    public bool HasValue { get; private set; }
    private string? _value;

    public void SetValue(string? value)
    {
        _value = value;
        HasValue = !string.IsNullOrEmpty(value);
        if (Options != null)
        {
            foreach (var opt in Options)
                opt.Selected = opt.Value?.ToString() == value;
        }
    }

    public string? Value()
    {
        if (!HasValue) return null;
        return _value;
    }

    public T? Value<T>()
    {
        if (!HasValue || _value == null) return default;
        var targetType = typeof(T);
        if (targetType.IsEnum)
            return (T)Enum.Parse(targetType, _value);
        return (T)Convert.ChangeType(_value, targetType);
    }

    public T[]? Values<T>()
    {
        if (!HasValue || _value == null) return Array.Empty<T>();
        return _value.Split(',').Select(v => (T)Convert.ChangeType(v, typeof(T))).ToArray();
    }
}

/// <summary>
/// 选项模型
/// </summary>
public class AdminOptions
{
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public object? Value { get; set; }
    public bool Selected { get; set; }
}

/// <summary>
/// 表格行包装
/// </summary>
public class AdminItem<T>
{
    public T Value { get; set; } = default!;
    public bool Selected { get; set; }
    public bool Disabled { get; set; }
    public int Level { get; set; } = 1;
    public bool Expanded { get; set; } = true;
    public string? RowClass { get; set; }
    public string? KeyString { get; set; }
}
