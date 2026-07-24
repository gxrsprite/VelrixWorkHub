using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>LMS 申请附件边界；存储、版本和审计仍由平台 AttachmentService 负责。</summary>
public sealed class LmsLicenseAttachmentService(
    ILmsLicenseRepository licenses,
    AttachmentService attachments,
    IAttachmentContentScanner? scanner = null,
    LmsLicenseAccessService? access = null)
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxFiles = 6;
    private const string BusinessType = nameof(LmsLicenseRequest);
    private IAttachmentContentScanner Scanner { get; } = scanner ?? new BasicAttachmentContentScanner();

    public IReadOnlyList<BusinessAttachment> List(Guid requestId, string actor, bool isAdministrator = false)
    {
        EnsureRequestAccess(requestId, actor, isAdministrator);
        return attachments.List(BusinessType, requestId);
    }

    public BusinessAttachment Register(Guid requestId, string fileName, string? contentType, ReadOnlySpan<byte> content, string actor, bool isAdministrator = false, string? otherInfo = null)
    {
        EnsureRequestCanChangeAttachments(requestId, actor, isAdministrator);
        EnsureFileAllowed(requestId, fileName, contentType, content.Length, actor);
        EnsureContentAllowed(fileName, contentType, content.ToArray());
        return attachments.Register(BusinessType, requestId, fileName, contentType, content, actor, otherInfo: otherInfo);
    }

    public async Task<BusinessAttachment> UploadAsync(Guid requestId, string fileName, string? contentType, Stream content, string actor, IAttachmentContentStore contentStore, bool isAdministrator = false, string? otherInfo = null, CancellationToken cancellationToken = default)
    {
        EnsureRequestCanChangeAttachments(requestId, actor, isAdministrator);
        EnsureFileNameAndType(fileName, contentType, actor);
        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes) throw new InvalidOperationException($"LMS 申请附件不能超过 {MaxFileBytes / 1024 / 1024} MB。");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        EnsureFileAllowed(requestId, fileName, contentType, buffer.Length, actor);
        EnsureContentAllowed(fileName, contentType, buffer.ToArray());
        buffer.Position = 0;
        return await attachments.UploadAsync(BusinessType, requestId, fileName, contentType, buffer, actor, contentStore, otherInfo: otherInfo, cancellationToken: cancellationToken);
    }

    private void EnsureContentAllowed(string fileName, string? contentType, ReadOnlyMemory<byte> content)
    {
        var result = Scanner.Scan(fileName, contentType, content);
        if (!result.IsAllowed) throw new InvalidOperationException(result.Reason ?? "附件内容扫描未通过。");
    }

    public void Delete(BusinessAttachment item, string actor, string reason, bool isAdministrator = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.BusinessType.Equals(BusinessType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("该附件不是 LMS 许可证申请附件。");
        EnsureRequestCanChangeAttachments(item.BusinessId, actor, isAdministrator);
        attachments.Delete(item, actor, reason);
    }

    private void EnsureRequestCanChangeAttachments(Guid requestId, string actor, bool isAdministrator)
    {
        var request = EnsureRequestAccess(requestId, actor, isAdministrator);
        if (request.Status is not (LmsLicenseRequestStatus.Draft or LmsLicenseRequestStatus.Submitted or LmsLicenseRequestStatus.Rejected or LmsLicenseRequestStatus.Withdrawn))
            throw new InvalidOperationException("当前许可证申请状态不允许变更附件。");
    }

    private LmsLicenseRequest EnsureRequestAccess(Guid requestId, string actor, bool isAdministrator)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new UnauthorizedAccessException("附件操作缺少操作人身份。");
        var request = licenses.ListRequests().FirstOrDefault(x => x.Id == requestId) ?? throw new InvalidOperationException("许可证申请不存在。");
        if (isAdministrator) return request;
        if (access is not null)
        {
            if (!access.CanReadRequest(requestId, actor, false)) throw new UnauthorizedAccessException("当前用户无权访问该许可证申请附件。");
        }
        else if (!request.Applicant.Equals(actor.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("当前用户无权访问该许可证申请附件。");
        return request;
    }

    private void EnsureFileAllowed(Guid requestId, string fileName, string? contentType, long sizeBytes, string actor)
    {
        EnsureFileNameAndType(fileName, contentType, actor);
        if (sizeBytes > MaxFileBytes) throw new InvalidOperationException($"LMS 申请附件不能超过 {MaxFileBytes / 1024 / 1024} MB。");
        if (attachments.List(BusinessType, requestId).Count >= MaxFiles) throw new InvalidOperationException($"每个 LMS 申请最多上传 {MaxFiles} 个附件。");
    }

    private static void EnsureFileNameAndType(string fileName, string? contentType, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new UnauthorizedAccessException("附件操作缺少操作人身份。");
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var normalizedType = string.IsNullOrWhiteSpace(contentType) ? string.Empty : contentType.Trim().ToLowerInvariant();
        var expected = extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };
        if (expected is null) throw new InvalidOperationException("LMS 申请附件扩展名不在允许列表内。");
        if (!normalizedType.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("LMS 申请附件 MIME 类型与扩展名不匹配。");
    }
}
