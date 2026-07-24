using System;
using System.Collections.Generic;
using BootstrapBlazor.Components;
using FreeSql.DataAnnotations;

/// <summary>
/// 用户
/// </summary>
public class SysUser : EntityCreated
{
	[Navigate(ManyToMany = typeof(SysRoleUser))]
	public List<SysRole> Roles { get; set; }

	[Navigate("UserId")]
	public List<SysRoleUser> RoleUsers { get; set; }

	/// <summary>
	/// 名称
	/// </summary>
	[Column(StringLength = 50)]
	public string Username { get; set; }

	/// <summary>
	/// 昵称
	/// </summary>
	[Column(StringLength = 50)]
	public string Nickname { get; set; }

	/// <summary>
	/// 密码
	/// </summary>
	[Column(StringLength = 50)]
	public string? Password { get; set; }

	/// <summary>
	/// PHC password hash. The legacy Password column is retained only for one-time migration.
	/// </summary>
	[Column(StringLength = 512)]
	public string? PasswordHash { get; set; }

	/// <summary>
	/// Incremented when credentials change to invalidate previously issued sessions.
	/// </summary>
	public int AuthVersion { get; set; }

	/// <summary>
	/// 是否可用
	/// </summary>
	public bool IsEnabled { get; set; }

	/// <summary>
	/// 登陆时间
	/// </summary>
	public DateTime LoginTime { get; set; }

	/// <summary>
	/// 所属组织
	/// </summary>
	public Guid OrgId { get; set; }

	[Navigate("OrgId")]
	public SysOrg Org { get; set; }

	/// <summary>
	/// 备注
	/// </summary>
	[Column(StringLength = 500)]
	public string Description { get; set; }

	/// <summary>
	/// 是否系统
	/// </summary>
	public bool IsSystem { get; set; }
}
