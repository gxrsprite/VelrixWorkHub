using System.ComponentModel;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 法定节假日/调休上班日
/// </summary>
public class SysHoliday : EntityCreated
{
    /// <summary>
    /// 日期
    /// </summary>
    [Column(Position = 3)]
    public DateTime Date { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Column(StringLength = 50)]
    public string Name { get; set; } = "";

    /// <summary>
    /// 类型
    /// </summary>
    public SysHolidayType Type { get; set; }

    /// <summary>
    /// 启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    [Column(StringLength = 500)]
    public string Description { get; set; } = "";
}

public enum SysHolidayType
{
    [Description("假日")]
    Holiday,

    [Description("调休上班")]
    Workday
}
