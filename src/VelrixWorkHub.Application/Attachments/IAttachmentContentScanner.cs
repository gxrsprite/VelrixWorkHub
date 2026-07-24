using System.Text;

namespace VelrixWorkHub.Application.Attachments;

public sealed record AttachmentContentScanResult(bool IsAllowed, string? Reason = null)
{
    public static AttachmentContentScanResult Allowed() => new(true);
    public static AttachmentContentScanResult Rejected(string reason) => new(false, reason);
}

/// <summary>附件内容扫描契约；生产环境可替换为病毒检测服务。</summary>
public interface IAttachmentContentScanner
{
    AttachmentContentScanResult Scan(string fileName, string? contentType, ReadOnlyMemory<byte> content);
}

/// <summary>轻量本地防护：阻止常见可执行伪装和脚本载荷，不替代专业病毒引擎。</summary>
public sealed class BasicAttachmentContentScanner : IAttachmentContentScanner
{
    public AttachmentContentScanResult Scan(string fileName, string? contentType, ReadOnlyMemory<byte> content)
    {
        var bytes = content.Span;
        if (bytes.Length >= 2 && bytes[0] == (byte)'M' && bytes[1] == (byte)'Z')
            return AttachmentContentScanResult.Rejected("附件内容疑似可执行文件，已拒绝上传。");

        var text = Encoding.UTF8.GetString(bytes[..Math.Min(bytes.Length, 1024 * 1024)]);
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("<?php", StringComparison.Ordinal)
            || normalized.Contains("<script", StringComparison.Ordinal)
            || normalized.Contains("powershell -", StringComparison.Ordinal)
            || normalized.Contains("cmd.exe", StringComparison.Ordinal))
            return AttachmentContentScanResult.Rejected("附件内容疑似脚本载荷，已拒绝上传。");

        return AttachmentContentScanResult.Allowed();
    }
}
