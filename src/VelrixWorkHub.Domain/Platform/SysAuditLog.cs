using FreeSql.DataAnnotations;

/// <summary>
/// 审核日志
/// </summary>
public class SysAuditLog : EntityCreated<Guid>
{
	/// <summary>
	/// 审核表名
	/// </summary>
	[Column(StringLength = 50)]
	public string TableName { get; set; }

	/// <summary>
	/// 审核表Id值
	/// </summary>
	public Guid TableId { get; set; }

	/// <summary>
	/// 步骤
	/// </summary>
	[Column(StringLength = 10)]
	public string Step { get; set; }

	/// <summary>
	/// 通过/退回/拒绝
	/// </summary>
	[Column(MapType = typeof(int))]
	public SysAuditStatus Result { get; set; }

	/// <summary>
	/// 意见
	/// </summary>
	[Column(StringLength = 500)]
	public string Opinion { get; set; }

	/// <summary>
	/// 备用
	/// </summary>
	[Column(StringLength = 50)]
	public string Tag { get; set; }

	/// <summary>
	/// 下一步
	/// </summary>
	[Column(StringLength = 10)]
	public string NextStep { get; set; }
}
