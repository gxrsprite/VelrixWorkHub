using System;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 登陆日志
/// </summary>
public class SysUserLoginLog : Entity
{
	public enum LogType
	{
		登陆成功,
		登陆失败
	}

	/// <summary>
	/// 登陆时间
	/// </summary>
	[Column(CanUpdate = false, ServerTime = DateTimeKind.Local)]
	public DateTime LoginTime { get; set; }

	/// <summary>
	/// 用户名
	/// </summary>
	[Column(StringLength = 50)]
	public string Username { get; set; }

	/// <summary>
	/// 日志类型
	/// </summary>
	public LogType Type { get; set; }

	/// <summary>
	/// IP
	/// </summary>
	[Column(StringLength = 50)]
	public string Ip { get; set; }

	/// <summary>
	/// 地点
	/// </summary>
	[Column(StringLength = 100)]
	public string City { get; set; }

	/// <summary>
	/// 浏览器
	/// </summary>
	public string Browser { get; set; }

	/// <summary>
	/// 操作系统
	/// </summary>
	[Column(StringLength = 50)]
	public string? OS { get; set; }

	/// <summary>
	/// 设备
	/// </summary>
	public WebClientDeviceType Device { get; set; }

	/// <summary>
	/// 浏览器语言
	/// </summary>
	[Column(StringLength = 50)]
	public string Language { get; set; }

	/// <summary>
	/// UserAgent
	/// </summary>
	[Column(StringLength = 500)]
	public string? UserAgent { get; set; }

	/// <summary>
	/// 浏览器引擎信息
	/// </summary>
	public string? Engine { get; set; }
}
