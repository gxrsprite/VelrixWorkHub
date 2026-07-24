namespace VelrixWorkHub.Domain;

public enum LmsLicenseProductStatus { Active, Disabled }

public sealed class LmsLicenseProduct
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public LmsLicenseProductStatus Status { get; private set; } = LmsLicenseProductStatus.Active;
    public DateTime CreatedAt { get; private set; }
    public LmsLicenseProduct(string code, string name, string? description, string? otherInfo, DateTime createdAt) { Edit(code, name, description, otherInfo); CreatedAt = createdAt; }
    public void Edit(string code, string name, string? description, string? otherInfo) { if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("产品编码和名称不能为空。"); Code = code.Trim(); Name = name.Trim(); Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(); OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo)); }
    public void SetStatus(LmsLicenseProductStatus status) => Status = status;
}
