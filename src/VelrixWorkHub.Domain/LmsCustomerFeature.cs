namespace VelrixWorkHub.Domain;

public enum LmsCustomerFeatureStatus { Active, Disabled }

/// <summary>客户级许可证特性授权基线，引用 CRM 客户和客户范围的特性版本。</summary>
public sealed class LmsCustomerFeature
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; }
    public Guid FeatureVersionId { get; }
    public DateTime? ExpiresAt { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsCustomerFeatureStatus Status { get; private set; } = LmsCustomerFeatureStatus.Active;
    public DateTime CreatedAt { get; }

    public LmsCustomerFeature(Guid customerId, Guid featureVersionId, DateTime? expiresAt, string? notes, string? otherInfo, DateTime createdAt)
    {
        if (customerId == Guid.Empty || featureVersionId == Guid.Empty) throw new ArgumentException("客户和特性版本不能为空。");
        CustomerId = customerId;
        FeatureVersionId = featureVersionId;
        Edit(expiresAt, notes, otherInfo);
        CreatedAt = createdAt;
    }

    public void Edit(DateTime? expiresAt, string? notes, string? otherInfo)
    {
        ExpiresAt = expiresAt;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(LmsCustomerFeatureStatus status) => Status = status;
}
