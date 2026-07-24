using System;

/// <summary>
/// 实体基类-创建信息
/// </summary>
public interface IEntityCreated
{
	/// <summary>
	/// 创建者用户Id
	/// </summary>
	Guid? CreatedUserId { get; set; }

	/// <summary>
	/// 创建者
	/// </summary>
	string CreatedUserName { get; set; }

	/// <summary>
	/// 创建时间
	/// </summary>
	DateTime? CreatedTime { get; set; }
}
