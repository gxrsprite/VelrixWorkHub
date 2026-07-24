using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum LmsLicenseRequestStatus { Draft, Submitted, Approved, Rejected, Withdrawn, Cancelled }
public enum LmsLicenseStatus { Active, Disabled, Expired, Revoked }
public enum LmsLicenseLifecycleAction { Disabled, Enabled, Revoked }
public enum LmsLicenseReplacementKind { Renewal, Reissue, MachineChange }
public enum LmsLicenseReplacementRequestStatus { Draft, Submitted, Approved, Rejected, Withdrawn }

/// <summary>许可证申请；只记录申请意图和审批状态，不在系统内生成密钥。</summary>
public sealed class LmsLicenseRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string RequestNo { get; private set; } = string.Empty;
    public string Applicant { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    /// <summary>CRM 客户权威引用；旧申请可为空并继续使用 CustomerName 历史展示值。</summary>
    public Guid? CustomerId { get; private set; }
    /// <summary>可选 CRM 联系人引用；必须属于 CustomerId。</summary>
    public Guid? ContactId { get; private set; }
    /// <summary>LMS 客户机台引用；存在时必须同时存在 CustomerId。</summary>
    public Guid? CustomerMachineId { get; private set; }
    public string? CustomerName { get; private set; }
    /// <summary>申请时的型号与运行环境快照，避免机台主数据后续编辑改变审批事实。</summary>
    public string? Model { get; private set; }
    public string? Environment { get; private set; }
    /// <summary>授权方约定的宽限天数；0 表示不设置宽限期。</summary>
    public int GracePeriodDays { get; private set; }
    public string FeaturesJson { get; private set; } = "[]";
    /// <summary>已选 LMS 特性版本 ID JSON 数组；旧申请保持空数组。</summary>
    public string FeatureVersionIdsJson { get; private set; } = "[]";
    public DateTime? RequestedExpiresAt { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsLicenseRequestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public LmsLicenseRequest(string requestNo, string applicant, string productName, string? customerName, string featuresJson, DateTime? requestedExpiresAt, string? otherInfo, DateTime createdAt, Guid? customerId = null, Guid? customerMachineId = null, string? featureVersionIdsJson = null, Guid? contactId = null, string? model = null, string? environment = null, int gracePeriodDays = 0)
    {
        if (string.IsNullOrWhiteSpace(requestNo) || string.IsNullOrWhiteSpace(applicant) || string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("许可证申请编号、申请人和产品不能为空。");
        if (customerMachineId is not null && customerId is null) throw new ArgumentException("关联机台时必须关联 CRM 客户。", nameof(customerId));
        if (gracePeriodDays < 0) throw new ArgumentOutOfRangeException(nameof(gracePeriodDays), "宽限天数不能为负数。");
        RequestNo = requestNo.Trim(); Applicant = applicant.Trim(); ProductName = productName.Trim(); CustomerId = customerId; ContactId = contactId; CustomerMachineId = customerMachineId; CustomerName = Clean(customerName); Model = Clean(model); Environment = Clean(environment); GracePeriodDays = gracePeriodDays;
        FeaturesJson = NormalizeArray(featuresJson, nameof(featuresJson)); FeatureVersionIdsJson = NormalizeArray(featureVersionIdsJson, nameof(featureVersionIdsJson)); OtherInfo = NormalizeObject(otherInfo, nameof(otherInfo)); RequestedExpiresAt = requestedExpiresAt; CreatedAt = createdAt; Status = LmsLicenseRequestStatus.Draft;
    }
    public void Submit() { if (Status is not (LmsLicenseRequestStatus.Draft or LmsLicenseRequestStatus.Rejected or LmsLicenseRequestStatus.Withdrawn)) throw new InvalidOperationException("当前许可证申请不能提交。"); Status = LmsLicenseRequestStatus.Submitted; }
    public void Cancel() { if (Status is not (LmsLicenseRequestStatus.Draft or LmsLicenseRequestStatus.Submitted)) throw new InvalidOperationException("只有草稿或审批中的许可证申请可以取消。"); Status = LmsLicenseRequestStatus.Cancelled; }
    public void SetStatus(LmsLicenseRequestStatus status) => Status = status;
    internal static string NormalizeArray(string? value, string name) { try { using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value); if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new ArgumentException("必须是 JSON 数组。", name); return doc.RootElement.GetRawText(); } catch (JsonException ex) { throw new ArgumentException("必须是有效 JSON。", name, ex); } }
    internal static string NormalizeObject(string? value, string name) => JsonObjectValue.Normalize(value, name);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>授权续期、重发或换机的审批原单；审批通过前不改变既有授权。</summary>
public sealed class LmsLicenseReplacementRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string RequestNo { get; private set; } = string.Empty;
    public Guid OriginalAuthorizationId { get; private set; }
    public LmsLicenseReplacementKind Kind { get; private set; }
    public Guid? TargetMachineId { get; private set; }
    public string LicenseNo { get; private set; } = string.Empty;
    public string ExternalLicense { get; private set; } = string.Empty;
    public DateTime? ExpiresAt { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public string Applicant { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public LmsLicenseReplacementRequestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public LmsLicenseReplacementRequest(string requestNo, Guid originalAuthorizationId, LmsLicenseReplacementKind kind, Guid? targetMachineId, string licenseNo, string externalLicense, DateTime? expiresAt, string? otherInfo, string applicant, string reason, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(requestNo) || string.IsNullOrWhiteSpace(licenseNo) || string.IsNullOrWhiteSpace(externalLicense) || string.IsNullOrWhiteSpace(applicant) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("替代申请编号、新授权编号、外部 License、申请人和原因不能为空。");
        if (originalAuthorizationId == Guid.Empty) throw new ArgumentException("原授权不能为空。", nameof(originalAuthorizationId));
        if (kind == LmsLicenseReplacementKind.MachineChange && targetMachineId is null) throw new ArgumentException("换机申请必须指定目标机台。", nameof(targetMachineId));
        if (kind != LmsLicenseReplacementKind.MachineChange && targetMachineId is not null) throw new ArgumentException("续期或重发申请不能指定目标机台。", nameof(targetMachineId));
        RequestNo = requestNo.Trim(); OriginalAuthorizationId = originalAuthorizationId; Kind = kind; TargetMachineId = targetMachineId; LicenseNo = licenseNo.Trim(); ExternalLicense = externalLicense.Trim(); ExpiresAt = expiresAt; OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo)); Applicant = applicant.Trim(); Reason = reason.Trim(); CreatedAt = createdAt; Status = LmsLicenseReplacementRequestStatus.Draft;
    }

    public void Submit()
    {
        if (Status is not (LmsLicenseReplacementRequestStatus.Draft or LmsLicenseReplacementRequestStatus.Rejected or LmsLicenseReplacementRequestStatus.Withdrawn))
            throw new InvalidOperationException("当前授权替代申请不能提交。");
        Status = LmsLicenseReplacementRequestStatus.Submitted;
    }

    public void SetStatus(LmsLicenseReplacementRequestStatus status) => Status = status;
}

/// <summary>外部系统提供的授权资产；不解释或生成 License 原文。</summary>
public sealed class LmsLicenseAuthorization
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid? RequestId { get; private set; }
    /// <summary>由授权替代审批产生时，关联对应的替代申请原单。</summary>
    public Guid? ReplacementRequestId { get; private set; }
    /// <summary>续期或重发时被替代的历史授权。</summary>
    public Guid? SupersedesAuthorizationId { get; private set; }
    public LmsLicenseReplacementKind? ReplacementKind { get; private set; }
    public string LicenseNo { get; private set; } = string.Empty;
    public string ExternalLicense { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? CustomerMachineId { get; private set; }
    public string? Model { get; private set; }
    public string? Environment { get; private set; }
    public int GracePeriodDays { get; private set; }
    public string FeaturesJson { get; private set; } = "[]";
    public string FeatureVersionIdsJson { get; private set; } = "[]";
    public DateTime? ExpiresAt { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsLicenseStatus Status { get; private set; } = LmsLicenseStatus.Active;
    public DateTime CreatedAt { get; private set; }
    public LmsLicenseAuthorization(Guid? requestId, string licenseNo, string externalLicense, string productName, string featuresJson, DateTime? expiresAt, string? otherInfo, DateTime createdAt, Guid? customerId = null, Guid? customerMachineId = null, string? featureVersionIdsJson = null, Guid? contactId = null, Guid? supersedesAuthorizationId = null, LmsLicenseReplacementKind? replacementKind = null, Guid? replacementRequestId = null, string? model = null, string? environment = null, int gracePeriodDays = 0)
    {
        if (string.IsNullOrWhiteSpace(licenseNo) || string.IsNullOrWhiteSpace(externalLicense) || string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("授权编号、外部 License 和产品不能为空。");
        if (customerMachineId is not null && customerId is null) throw new ArgumentException("关联机台时必须关联 CRM 客户。", nameof(customerId));
        if (supersedesAuthorizationId is not null && replacementKind is null) throw new ArgumentException("替代历史授权时必须指定类型。", nameof(replacementKind));
        if (gracePeriodDays < 0) throw new ArgumentOutOfRangeException(nameof(gracePeriodDays), "宽限天数不能为负数。");
        RequestId = requestId; ReplacementRequestId = replacementRequestId; SupersedesAuthorizationId = supersedesAuthorizationId; ReplacementKind = replacementKind; LicenseNo = licenseNo.Trim(); ExternalLicense = externalLicense.Trim(); ProductName = productName.Trim(); CustomerId = customerId; ContactId = contactId; CustomerMachineId = customerMachineId; Model = Clean(model); Environment = Clean(environment); GracePeriodDays = gracePeriodDays; FeaturesJson = LmsLicenseRequest.NormalizeArray(featuresJson, nameof(featuresJson)); FeatureVersionIdsJson = LmsLicenseRequest.NormalizeArray(featureVersionIdsJson, nameof(featureVersionIdsJson)); OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo)); ExpiresAt = expiresAt; CreatedAt = createdAt;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    /// <summary>到期是时间派生状态，不改写原始人工状态，避免查询副作用覆盖授权历史。</summary>
    public DateTime? EffectiveExpiresAt
    {
        get
        {
            if (ExpiresAt is not DateTime expiresAt) return null;
            try { return expiresAt.AddDays(GracePeriodDays); }
            catch (ArgumentOutOfRangeException) { return DateTime.MaxValue; }
        }
    }
    public bool IsWithinGracePeriod(DateTime now) => Status == LmsLicenseStatus.Active
        && ExpiresAt is DateTime expiresAt
        && expiresAt < now
        && EffectiveExpiresAt >= now;
    public LmsLicenseStatus GetEffectiveStatus(DateTime now) => Status == LmsLicenseStatus.Active && EffectiveExpiresAt is DateTime effectiveExpiresAt && effectiveExpiresAt < now ? LmsLicenseStatus.Expired : Status;
    public void SetStatus(LmsLicenseStatus status) => Status = status;
    public void Disable(string actor, string reason)
    {
        EnsureActorAndReason(actor, reason);
        if (Status != LmsLicenseStatus.Active) throw new InvalidOperationException("只有有效授权可以停用。");
        Status = LmsLicenseStatus.Disabled;
    }
    public void Enable(string actor, string reason, DateTime now)
    {
        EnsureActorAndReason(actor, reason);
        if (Status != LmsLicenseStatus.Disabled) throw new InvalidOperationException("只有已停用授权可以重新开启。");
        if (ExpiresAt is DateTime expiresAt && expiresAt < now) throw new InvalidOperationException("已到期授权不能重新开启。");
        Status = LmsLicenseStatus.Active;
    }
    public void Revoke(string actor, string reason)
    {
        EnsureActorAndReason(actor, reason);
        if (Status == LmsLicenseStatus.Revoked) throw new InvalidOperationException("授权已作废，不能重复作废。");
        Status = LmsLicenseStatus.Revoked;
    }
    private static void EnsureActorAndReason(string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("生命周期变更原因不能为空。", nameof(reason));
    }
}

/// <summary>许可证人工生命周期操作的不可变审计记录；到期状态仍由查询按时间派生。</summary>
public sealed class LmsLicenseLifecycleEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AuthorizationId { get; }
    public LmsLicenseLifecycleAction Action { get; }
    public LmsLicenseStatus PreviousStatus { get; }
    public LmsLicenseStatus CurrentStatus { get; }
    public string Actor { get; }
    public string Reason { get; }
    public DateTime OccurredAt { get; }
    public LmsLicenseLifecycleEntry(Guid authorizationId, LmsLicenseLifecycleAction action, LmsLicenseStatus previousStatus, LmsLicenseStatus currentStatus, string actor, string reason, DateTime occurredAt)
    {
        if (authorizationId == Guid.Empty) throw new ArgumentException("授权不能为空。", nameof(authorizationId));
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("操作者和原因不能为空。");
        AuthorizationId = authorizationId; Action = action; PreviousStatus = previousStatus; CurrentStatus = currentStatus; Actor = actor.Trim(); Reason = reason.Trim(); OccurredAt = occurredAt;
    }
}
