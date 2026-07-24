namespace VelrixWorkHub.Domain;

public enum BusinessAttachmentStatus { Active, Deleted }
public enum AttachmentAuditAction { Uploaded, Downloaded, Deleted }

public sealed class BusinessAttachment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string BusinessType { get; private set; } = string.Empty;
    public Guid BusinessId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = "application/octet-stream";
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public string UploadedBy { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public BusinessAttachmentStatus Status { get; private set; } = BusinessAttachmentStatus.Active;
    public string DeletedReason { get; private set; } = string.Empty;
    public DateTime? DeletedAt { get; private set; }

    public BusinessAttachment(string businessType, Guid businessId, string fileName, string? contentType, long sizeBytes, string sha256, string storageKey, int versionNumber, string uploadedBy, DateTime uploadedAt, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(businessType)) throw new ArgumentException("业务对象类型不能为空。", nameof(businessType));
        if (businessId == Guid.Empty) throw new ArgumentException("业务对象不能为空。", nameof(businessId));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名不能为空。", nameof(fileName));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes), "文件大小不能为负数。");
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit)) throw new ArgumentException("文件哈希必须是 64 位十六进制 SHA-256。", nameof(sha256));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("存储键不能为空。", nameof(storageKey));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber), "附件版本必须从 1 开始。");
        if (string.IsNullOrWhiteSpace(uploadedBy)) throw new ArgumentException("上传人不能为空。", nameof(uploadedBy));
        var normalizedFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(normalizedFileName) || normalizedFileName is "." or "..") throw new ArgumentException("文件名无效。", nameof(fileName));
        BusinessType = businessType.Trim(); BusinessId = businessId; FileName = normalizedFileName; ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(); SizeBytes = sizeBytes; Sha256 = sha256.Trim().ToLowerInvariant(); StorageKey = storageKey.Trim(); VersionNumber = versionNumber; UploadedBy = uploadedBy.Trim(); UploadedAt = uploadedAt; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Delete(string reason, DateTime deletedAt)
    {
        if (Status == BusinessAttachmentStatus.Deleted) throw new InvalidOperationException("附件已经删除。");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("删除原因不能为空。", nameof(reason));
        Status = BusinessAttachmentStatus.Deleted; DeletedReason = reason.Trim(); DeletedAt = deletedAt;
    }
}

public sealed class AttachmentAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AttachmentId { get; init; }
    public string BusinessType { get; init; } = string.Empty;
    public Guid BusinessId { get; init; }
    public AttachmentAuditAction Action { get; init; }
    public string Actor { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string Details { get; init; } = string.Empty;

    public AttachmentAuditEntry(Guid attachmentId, string businessType, Guid businessId, AttachmentAuditAction action, string actor, DateTime occurredAt, string? details = null)
    {
        if (attachmentId == Guid.Empty) throw new ArgumentException("附件不能为空。", nameof(attachmentId));
        if (string.IsNullOrWhiteSpace(businessType)) throw new ArgumentException("业务对象类型不能为空。", nameof(businessType));
        if (businessId == Guid.Empty) throw new ArgumentException("业务对象不能为空。", nameof(businessId));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作人不能为空。", nameof(actor));
        AttachmentId = attachmentId; BusinessType = businessType.Trim(); BusinessId = businessId; Action = action; Actor = actor.Trim(); OccurredAt = occurredAt; Details = details?.Trim() ?? string.Empty;
    }
}
