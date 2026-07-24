using System;

/// <summary>
/// 实体基类-修改信息
/// </summary>
public interface IEntityModified
{
	/// <summary>
	/// 修改者Id
	/// </summary>
	Guid? ModifiedUserId { get; set; }

	/// <summary>
	/// 修改者
	/// </summary>
	string ModifiedUserName { get; set; }

	/// <summary>
	/// 修改时间
	/// </summary>
	DateTime? ModifiedTime { get; set; }
}
