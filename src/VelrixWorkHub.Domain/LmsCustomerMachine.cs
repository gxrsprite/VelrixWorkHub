namespace VelrixWorkHub.Domain;

public enum LmsCustomerMachineStatus { Active, Disabled }

/// <summary>客户侧受许可证约束的机台；客户名称始终由 CRM 主数据解析。</summary>
public sealed class LmsCustomerMachine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; }
    public string MachineCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public string? Environment { get; private set; }
    public LmsCustomerMachineStatus Status { get; private set; } = LmsCustomerMachineStatus.Active;
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; }

    public LmsCustomerMachine(Guid customerId, string machineCode, string productName, string? model, string? environment, string? otherInfo, DateTime createdAt)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("客户不能为空。", nameof(customerId));
        CustomerId = customerId;
        Edit(machineCode, productName, model, environment, otherInfo);
        CreatedAt = createdAt;
    }

    public void Edit(string machineCode, string productName, string? model, string? environment, string? otherInfo)
    {
        if (string.IsNullOrWhiteSpace(machineCode) || string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("机器码和许可证产品不能为空。");
        MachineCode = machineCode.Trim();
        ProductName = productName.Trim();
        Model = Clean(model);
        Environment = Clean(environment);
        OtherInfo = LmsLicenseRequest.NormalizeObject(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(LmsCustomerMachineStatus status) => Status = status;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
