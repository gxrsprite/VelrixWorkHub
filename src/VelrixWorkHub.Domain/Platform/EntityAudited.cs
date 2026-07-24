using FreeSql.DataAnnotations;

/// <summary>
/// 审核状态
/// </summary>
public enum SysAuditStatus
{
    待提交,
    审核中,
    退回,
    通过,
    拒绝
}

/// <summary>
/// 实体基类-审核信息
/// </summary>
[Table(DisableSyncStructure = true)]
public abstract class EntityAudited : EntityModified<Guid>
{
	/// <summary>
	/// 审核状态
	/// </summary>
	[Column(Position = -19, MapType = typeof(int), CanUpdate = false)]
	public virtual SysAuditStatus AuditStatus { get; set; }

	/// <summary>
	/// 审核步骤
	/// </summary>
	[Column(Position = -18, StringLength = 10, CanUpdate = false)]
	public virtual string AuditStep { get; set; }

	/// <summary>
	/// 审核版本
	/// </summary>
	[Column(Position = -17, CanUpdate = false)]
	public virtual int AuditVersion { get; set; }
}
