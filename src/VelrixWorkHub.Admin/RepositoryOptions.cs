using System;
using FreeSql;

internal class RepositoryOptions
{
	/// <summary>
	/// 使用无参数化设置（对应 IInsert/IUpdate）
	/// </summary>
	public bool? NoneParameter { get; set; }

	/// <summary>
	/// 是否开启 IFreeSql GlobalFilter 功能（默认：true）
	/// </summary>
	public bool EnableGlobalFilter { get; set; } = true;

	/// <summary>
	/// DbContext/Repository 审计值事件，适合 Scoped IOC 中获取登陆信息
	/// </summary>
	public Action<DbContextAuditValueEventArgs> AuditValue { get; set; }
}
