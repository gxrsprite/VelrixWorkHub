using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 数据字典
/// </summary>
public class SysDict : Entity
{
	/// <summary>
	/// 字典类型
	/// </summary>
	public Guid ParentId { get; set; }

	[Navigate("ParentId")]
	public SysDict Parent { get; set; }

	/// <summary>
	/// 编码
	/// </summary>
	[Column(StringLength = 50)]
	public string Name { get; set; }

	/// <summary>
	/// 值
	/// </summary>
	[Column(StringLength = 50)]
	public string Value { get; set; }

	/// <summary>
	/// 值2
	/// </summary>
	[Column(StringLength = 50)]
	public string Value2 { get; set; }

	/// <summary>
	/// 值3
	/// </summary>
	[Column(StringLength = 50)]
	public string Value3 { get; set; }

	/// <summary>
	/// 值4
	/// </summary>
	[Column(StringLength = 50)]
	public string Value4 { get; set; }

	/// <summary>
	/// 值5
	/// </summary>
	[Column(StringLength = 50)]
	public string Value5 { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }

	/// <summary>
	/// 启用
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// 排序
	/// </summary>
	public int Sort { get; set; }
}
