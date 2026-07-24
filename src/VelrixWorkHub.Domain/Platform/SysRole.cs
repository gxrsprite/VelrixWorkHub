using System.Collections.Generic;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 角色
/// </summary>
public class SysRole : Entity
{
	/// <summary>
	/// 名称
	/// </summary>
	[Column(StringLength = 50)]
	public string Name { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }

	/// <summary>
	/// 系统
	/// </summary>
	public bool IsAdministrator { get; set; }

	[Navigate(ManyToMany = typeof(SysRoleUser))]
	public List<SysUser> Users { get; set; }

	[Navigate(ManyToMany = typeof(SysRoleMenu))]
	public List<SysMenu> Menus { get; set; }
}
