using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsCustomerMachineService(
    ILmsCustomerMachineRepository repository,
    CustomerService customers,
    LmsLicenseProductService products)
{
    public IReadOnlyList<LmsCustomerMachine> List(Guid? customerId = null, bool includeDisabled = true) =>
        repository.List()
            .Where(x => customerId is null || x.CustomerId == customerId)
            .Where(x => includeDisabled || x.Status == LmsCustomerMachineStatus.Active)
            .OrderBy(x => x.MachineCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public LmsCustomerMachine Create(Guid customerId, string machineCode, string productName, string? model, string? environment, string? otherInfo)
    {
        EnsureActiveCustomer(customerId);
        products.EnsureActiveProductName(productName);
        var item = new LmsCustomerMachine(customerId, machineCode, productName, model, environment, otherInfo, DateTime.Now);
        if (repository.List().Any(x => x.MachineCode.Equals(item.MachineCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("机器码已存在。");
        }
        repository.Add(item);
        return item;
    }

    public void Edit(LmsCustomerMachine item, string machineCode, string productName, string? model, string? environment, string? otherInfo)
    {
        EnsureActiveCustomer(item.CustomerId);
        products.EnsureActiveProductName(productName);
        var normalizedCode = machineCode.Trim();
        if (repository.List().Any(x => x.Id != item.Id && x.MachineCode.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("机器码已存在。");
        }
        item.Edit(machineCode, productName, model, environment, otherInfo);
        repository.Update(item);
    }

    public void SetStatus(LmsCustomerMachine item, LmsCustomerMachineStatus status)
    {
        item.SetStatus(status);
        repository.Update(item);
    }

    private void EnsureActiveCustomer(Guid customerId)
    {
        var customer = customers.List().SingleOrDefault(x => x.Id == customerId)
            ?? throw new InvalidOperationException("CRM 客户不存在。");
        if (customer.Status != CustomerStatus.Active) throw new InvalidOperationException("停用的 CRM 客户不能新增或编辑机台。");
    }
}
