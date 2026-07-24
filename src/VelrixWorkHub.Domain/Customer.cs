namespace VelrixWorkHub.Domain;
public enum CustomerStatus { Active, Inactive }
public sealed class Customer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public CustomerStatus Status { get; private set; }
    public Customer(string name, string? contactName = null, string? phone = null, string? email = null, string? notes = null, string? otherInfo = null) { Edit(name, contactName, phone, email, notes, otherInfo); Status = CustomerStatus.Active; }
    public void Edit(string name, string? contactName, string? phone, string? email, string? notes, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("客户名称不能为空。", nameof(name));
        Name = name.Trim(); ContactName = Clean(contactName); Phone = Clean(phone); Email = Clean(email); Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
    public void SetActive(bool active) => Status = active ? CustomerStatus.Active : CustomerStatus.Inactive;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
