using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.FollowUps;
using VelrixWorkHub.Application.MasterData;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Customers;
public sealed class CustomerService(
    ICustomerRepository repository,
    ICustomerContactRepository? contactRepository = null,
    ICustomerFollowUpRepository? followUpRepository = null,
    ISalesContractRepository? contractRepository = null,
    ISalesOrderRepository? salesOrderRepository = null,
    IPmsProjectRepository? projectRepository = null,
    ISettlementRepository? settlementRepository = null,
    LmsCustomerReferenceService? lmsReferences = null)
{
    public IReadOnlyList<Customer> List(string? keyword = null, CustomerFilter filter = CustomerFilter.All)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(item => item.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || (item.ContactName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) || (item.Phone?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        query = filter switch { CustomerFilter.Active => query.Where(item => item.Status == CustomerStatus.Active), CustomerFilter.Inactive => query.Where(item => item.Status == CustomerStatus.Inactive), _ => query };
        return query.ToArray();
    }
    public int Count(CustomerFilter filter) => List(filter: filter).Count;
    public Customer Create(string name, string? contactName, string? phone, string? email, string? notes, string? otherInfo = null) { var item = new Customer(name, contactName, phone, email, notes, otherInfo); repository.Add(item); return item; }
    public void Edit(Customer item, string name, string? contactName, string? phone, string? email, string? notes, string? otherInfo = null) { item.Edit(name, contactName, phone, email, notes, otherInfo); repository.Update(item); }
    public void SetActive(Customer item, bool active) { item.SetActive(active); repository.Update(item); }
    public void Remove(Customer item)
    {
        var impact = MasterDataImpactService.Customer(
            item.Id,
            contactRepository?.List() ?? Array.Empty<CustomerContact>(),
            followUpRepository?.List() ?? Array.Empty<CustomerFollowUp>(),
            contractRepository?.List() ?? Array.Empty<SalesContract>(),
            salesOrderRepository?.List() ?? Array.Empty<SalesOrder>(),
            projectRepository?.List() ?? Array.Empty<PmsProject>(),
            settlementRepository?.List() ?? Array.Empty<ErpSettlement>());
        var lmsImpact = lmsReferences?.GetImpact(item.Id);
        var decision = MasterDataImpactService.Decide(
            "客户",
            ("联系人", impact.ContactReferenceCount),
            ("跟进", impact.FollowUpReferenceCount),
            ("合同", impact.ContractReferenceCount),
            ("销售订单", impact.SalesOrderReferenceCount),
            ("项目", impact.ProjectReferenceCount),
            ("核销", impact.SettlementReferenceCount),
            ("LMS 客户机台", lmsImpact?.MachineReferenceCount ?? 0),
            ("LMS 客户特性", lmsImpact?.CustomerFeatureReferenceCount ?? 0),
            ("LMS 机台特性", lmsImpact?.MachineFeatureReferenceCount ?? 0),
            ("LMS 许可证申请", lmsImpact?.LicenseRequestReferenceCount ?? 0),
            ("LMS 许可证授权", lmsImpact?.AuthorizationReferenceCount ?? 0));
        if (!decision.CanDelete) throw new InvalidOperationException($"{decision.Reason}{decision.SuggestedAction}");
        repository.Remove(item.Id);
    }
}
