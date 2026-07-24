using FreeSql;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 数据库
/// </summary>
public class SysTenantDatabase : EntityModified
{
	/// <summary>
	/// 显示名
	/// </summary>
	[Column(StringLength = 50)]
	public string Label { get; set; }

	/// <summary>
	/// 类型
	/// </summary>
	public DataType DataType { get; set; }

	/// <summary>
	/// 连接串
	/// </summary>
	[Column(StringLength = 500)]
	public string ConenctionString { get; set; }
}
