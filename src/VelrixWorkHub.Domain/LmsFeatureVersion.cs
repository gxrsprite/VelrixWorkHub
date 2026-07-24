namespace VelrixWorkHub.Domain;

public enum LmsFeatureLevel { Basic, Intermediate, Advanced }
public enum LmsFeatureScope { Customer, Machine }
public enum LmsFeatureVersionStatus { Active, Disabled }

public sealed class LmsFeatureVersion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid FeatureId { get; }
    public string Version { get; private set; } = string.Empty;
    public LmsFeatureLevel Level { get; private set; }
    public LmsFeatureScope Scope { get; private set; }
    public LmsFeatureVersionStatus Status { get; private set; } = LmsFeatureVersionStatus.Active;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; }
    public LmsFeatureVersion(Guid featureId, string version, LmsFeatureLevel level, LmsFeatureScope scope, string? otherInfo, DateTime createdAt)
    { if (featureId == Guid.Empty || string.IsNullOrWhiteSpace(version)) throw new ArgumentException("特性和版本号不能为空。"); FeatureId = featureId; Version = version.Trim(); Level = level; Scope = scope; OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo)); CreatedAt = createdAt; }
    public void SetStatus(LmsFeatureVersionStatus status) => Status = status;
}
