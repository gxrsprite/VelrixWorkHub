namespace VelrixWorkHub.Domain;

public sealed class Supplier
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public SupplierStatus Status { get; private set; }
    public SupplierQualificationStatus QualificationStatus { get; private set; } = SupplierQualificationStatus.Qualified;
    public Supplier(string code, string name, string? contactName, string? phone, string? notes, string? otherInfo = null)
    { Edit(code, name, contactName, phone, notes, otherInfo); Status = SupplierStatus.Active; }
    public void Edit(string code, string name, string? contactName, string? phone, string? notes, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("供应商编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("供应商名称不能为空。", nameof(name));
        Code = code.Trim(); Name = name.Trim(); ContactName = Clean(contactName); Phone = Clean(phone); Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
    public void SetActive(bool active) => Status = active ? SupplierStatus.Active : SupplierStatus.Inactive;
    public void SetQualification(SupplierQualificationStatus status) => QualificationStatus = status;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
public enum SupplierStatus { Inactive, Active }
// Qualified is intentionally zero so existing rows without this column remain usable after schema sync.
public enum SupplierQualificationStatus { Qualified, Pending, Suspended }
