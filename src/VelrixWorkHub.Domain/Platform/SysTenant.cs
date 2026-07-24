using System.Collections.Generic;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 租户
/// </summary>
public class SysTenant : EntityCreated<string>
{
	/// <summary>
	/// 名称
	/// </summary>
	[Column(StringLength = 50)]
	public override string Id { get; set; }

	/// <summary>
	/// 数据库模板
	/// </summary>
	public Guid DatabaseId { get; set; }

	public SysTenantDatabase Database { get; set; }

	/// <summary>
	/// 数据库模板(定时任务)
	/// </summary>
	public Guid TaskDatabaseId { get; set; }

	public SysTenantDatabase TaskDatabase { get; set; }

	/// <summary>
	/// 域名
	/// </summary>
	[Column(StringLength = 50)]
	public string Host { get; set; }

	[Column(StringLength = 50)]
	public string Host2 { get; set; }

	[Column(StringLength = 50)]
	public string Host3 { get; set; }

	/// <summary>
	/// 标题
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }

	/// <summary>
	/// LOGO
	/// </summary>
	[Column(StringLength = 128)]
	public string Logo { get; set; }

	/// <summary>
	/// 登陆页图片
	/// </summary>
	[Column(StringLength = 128)]
	public string LoginImage { get; set; }

	/// <summary>
	/// 是否启用
	/// </summary>
	public bool IsEnabled { get; set; }

	[Navigate(ManyToMany = typeof(SysTenantMenu))]
	public List<SysMenu> Menus { get; set; }
}
