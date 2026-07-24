namespace VelrixWorkHub.Domain;

public enum LmsMachineFeatureStatus { Active, Disabled }

/// <summary>指定客户机台上的特性细化或禁用记录；不得独立扩展客户基线。</summary>
public sealed class LmsMachineFeature
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerMachineId { get; }
    public Guid FeatureVersionId { get; }
    public DateTime? ExpiresAt { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsMachineFeatureStatus Status { get; private set; } = LmsMachineFeatureStatus.Active;
    public DateTime CreatedAt { get; }

    public LmsMachineFeature(Guid customerMachineId, Guid featureVersionId, DateTime? expiresAt, string? notes, string? otherInfo, DateTime createdAt)
    {
        if (customerMachineId == Guid.Empty || featureVersionId == Guid.Empty) throw new ArgumentException("客户机台和特性版本不能为空。");
        CustomerMachineId = customerMachineId;
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

    public void SetStatus(LmsMachineFeatureStatus status) => Status = status;
}
