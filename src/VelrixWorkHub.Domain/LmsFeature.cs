namespace VelrixWorkHub.Domain;

public enum LmsFeatureStatus { Active, Disabled }

/// <summary>许可证特性定义；版本与客户/机台适用范围在后续主数据切片中关联。</summary>
public sealed class LmsFeature
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsFeatureStatus Status { get; private set; } = LmsFeatureStatus.Active;
    public DateTime CreatedAt { get; private set; }
    public LmsFeature(string code, string name, string? description, string? otherInfo, DateTime createdAt) { Edit(code, name, description, otherInfo); CreatedAt = createdAt; }
    public void Edit(string code, string name, string? description, string? otherInfo) { if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("特性编码和名称不能为空。"); Code = code.Trim(); Name = name.Trim(); Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(); OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo)); }
    public void SetStatus(LmsFeatureStatus status) => Status = status;
}
