using System.Collections.Generic;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 组织结构
/// </summary>
public class SysOrg : Entity
{
	public enum OrgType
	{
		集团,
		公司,
		部门,
		供应商,
		客户
	}

	[Navigate("ParentId")]
	public SysOrg Parent { get; set; }

	[Navigate("ParentId")]
	public List<SysOrg> Children { get; set; }

	/// <summary>
	/// 组织名称
	/// </summary>
	public string Label { get; set; }

	/// <summary>
	/// 组织类型
	/// </summary>
	public OrgType Type { get; set; }

	/// <summary>
	/// 父级菜单
	/// </summary>
	public Guid ParentId { get; set; }

	/// <summary>
	/// 排序
	/// </summary>
	public int Sort { get; set; }

	/// <summary>
	/// 可用
	/// </summary>
	public bool IsEnabled { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }
}
