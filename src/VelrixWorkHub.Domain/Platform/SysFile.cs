using System;
using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 文件
/// </summary>
public class SysFile : EntityCreated
{
	/// <summary>
	/// OSS供应商
	/// </summary>
	[Column(MapType = typeof(string), StringLength = 50)]
	public string? Provider { get; set; }

	/// <summary>
	/// 存储桶名称
	/// </summary>
	[Column(StringLength = 200)]
	public string BucketName { get; set; }

	/// <summary>
	/// 文件目录
	/// </summary>
	[Column(StringLength = 500)]
	public string FileDirectory { get; set; }

	/// <summary>
	/// 文件Guid
	/// </summary>
	public Guid FileGuid { get; set; }

	/// <summary>
	/// 保存文件名
	/// </summary>
	[Column(StringLength = 200)]
	public string SaveFileName { get; set; }

	/// <summary>
	/// 文件名
	/// </summary>
	[Column(StringLength = 200)]
	public string OriginFileName { get; set; }

	/// <summary>
	/// 文件扩展名
	/// </summary>
	[Column(StringLength = 20)]
	public string Extension { get; set; }

	/// <summary>
	/// 文件字节长度
	/// </summary>
	public long Size { get; set; }

	/// <summary>
	/// 文件大小格式化
	/// </summary>
	[Column(StringLength = 50)]
	public string SizeFormat { get; set; }

	/// <summary>
	/// 链接地址
	/// </summary>
	[Column(StringLength = 500)]
	public string LinkUrl { get; set; }

	/// <summary>
	/// SHA-256内容哈希
	/// </summary>
	[Column(StringLength = 64)]
	public string Sha256 { get; set; } = string.Empty;

	[Column(IsIgnore = true)]
	public bool IsSelect { get; set; }
}
