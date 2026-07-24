namespace VelrixWorkHub.Domain;
public sealed class CustomerContact
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Position { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsPrimary { get; private set; }
    public CustomerContact(Guid customerId, string name, string? position = null, string? phone = null, string? email = null, bool isPrimary = false) { Edit(customerId, name, position, phone, email); IsPrimary = isPrimary; }
    public void Edit(Guid customerId, string name, string? position, string? phone, string? email)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("必须选择所属客户。", nameof(customerId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("联系人姓名不能为空。", nameof(name));
        CustomerId = customerId; Name = name.Trim(); Position = Clean(position); Phone = Clean(phone); Email = Clean(email);
    }
    public void SetPrimary(bool value) => IsPrimary = value;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
