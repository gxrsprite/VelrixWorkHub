using System.Collections.Generic;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 菜单
/// </summary>
public class SysMenu : EntityCreated
{
	[Navigate("ParentId")]
	public SysMenu Parent { get; set; }

	[Navigate("ParentId")]
	public List<SysMenu> Children { get; set; }

	/// <summary>
	/// 父级菜单
	/// </summary>
	public Guid ParentId { get; set; }

	/// <summary>
	/// 名称
	/// </summary>
	[Column(StringLength = 50)]
	public string Label { get; set; }

	/// <summary>
	/// 图标
	/// </summary>
	[Column(StringLength = 50)]
	public string Icon { get; set; }

	/// <summary>
	/// 路径
	/// </summary>
	[Column(StringLength = 50)]
	public string Path { get; set; }

	/// <summary>
	/// 排序
	/// </summary>
	public int Sort { get; set; }

	/// <summary>
	/// 类型
	/// </summary>
	public SysMenuType Type { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }

	/// <summary>
	/// 菜单样式
	/// </summary>
	public SysMenuSidebarStyle SidebarStyle { get; set; }

	/// <summary>
	/// 是否系统
	/// </summary>
	public bool IsSystem { get; set; }

	/// <summary>
	/// 隐藏
	/// </summary>
	public bool IsHidden { get; set; }

	[Navigate(ManyToMany = typeof(SysRoleMenu))]
	public List<SysRole> Roles { get; set; }

	[Navigate("MenuId")]
	public List<SysRoleMenu> RoleMenus { get; set; }

	[Navigate(ManyToMany = typeof(SysTenantMenu))]
	public List<SysTenant> Tenants { get; set; }
}
