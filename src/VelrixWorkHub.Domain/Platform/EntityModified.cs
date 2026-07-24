using System;
using FreeSql.DataAnnotations;

/// <summary>
/// 实体基类-修改信息
/// </summary>
[Table(DisableSyncStructure = true)]
public abstract class EntityModified<TKey> : EntityCreated<TKey>, IEntityModified
{
	/// <summary>
	/// 修改者Id
	/// </summary>
	[Column(Position = -12, CanInsert = false)]
	public virtual Guid? ModifiedUserId { get; set; }

	/// <summary>
	/// 修改者
	/// </summary>
	[Column(Position = -11, CanUpdate = false, StringLength = 50)]
	public virtual string ModifiedUserName { get; set; }

	/// <summary>
	/// 修改时间
	/// </summary>
	[Column(Position = -10, CanInsert = false, ServerTime = DateTimeKind.Local)]
	public virtual DateTime? ModifiedTime { get; set; }
}
/// <summary>
/// 实体基类-修改信息
/// </summary>
[Table(DisableSyncStructure = true)]
public abstract class EntityModified : EntityModified<Guid>
{
}
