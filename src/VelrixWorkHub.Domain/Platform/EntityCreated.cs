using System;
using FreeSql.DataAnnotations;

/// <summary>
/// 实体基类-创建信息
/// </summary>
[Table(DisableSyncStructure = true)]
public abstract class EntityCreated<TKey> : Entity<TKey>, IEntityCreated
{
	/// <summary>
	/// 创建者Id
	/// </summary>
	[Column(Position = -22, CanUpdate = false)]
	public virtual Guid? CreatedUserId { get; set; }

	/// <summary>
	/// 创建者
	/// </summary>
	[Column(Position = -21, CanUpdate = false, StringLength = 50)]
	public virtual string CreatedUserName { get; set; }

	/// <summary>
	/// 创建时间
	/// </summary>
	[Column(Position = -20, CanUpdate = false, ServerTime = DateTimeKind.Local)]
	public virtual DateTime? CreatedTime { get; set; }
}
/// <summary>
/// 实体基类-创建信息
/// </summary>
[Table(DisableSyncStructure = true)]
public abstract class EntityCreated : EntityCreated<Guid>
{
}
