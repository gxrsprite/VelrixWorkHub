using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsCustomerFeatureService(
    ILmsCustomerFeatureRepository repository,
    CustomerService customers,
    LmsFeatureVersionService featureVersions)
{
    public IReadOnlyList<LmsCustomerFeature> List(Guid? customerId = null, bool includeDisabled = true) =>
        repository.List()
            .Where(x => customerId is null || x.CustomerId == customerId)
            .Where(x => includeDisabled || x.Status == LmsCustomerFeatureStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();

    public LmsCustomerFeature Create(Guid customerId, Guid featureVersionId, DateTime? expiresAt, string? notes, string? otherInfo)
    {
        EnsureActiveCustomer(customerId);
        EnsureCustomerFeatureVersion(featureVersionId);
        if (repository.List().Any(x => x.CustomerId == customerId && x.FeatureVersionId == featureVersionId))
        {
            throw new InvalidOperationException("该客户已拥有此特性版本授权基线。");
        }
        var item = new LmsCustomerFeature(customerId, featureVersionId, expiresAt, notes, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(LmsCustomerFeature item, DateTime? expiresAt, string? notes, string? otherInfo)
    {
        EnsureActiveCustomer(item.CustomerId);
        EnsureCustomerFeatureVersion(item.FeatureVersionId);
        item.Edit(expiresAt, notes, otherInfo);
        repository.Update(item);
    }

    public void SetStatus(LmsCustomerFeature item, LmsCustomerFeatureStatus status)
    {
        item.SetStatus(status);
        repository.Update(item);
    }

    private void EnsureActiveCustomer(Guid customerId)
    {
        var customer = customers.List().SingleOrDefault(x => x.Id == customerId) ?? throw new InvalidOperationException("CRM 客户不存在。");
        if (customer.Status != CustomerStatus.Active) throw new InvalidOperationException("停用的 CRM 客户不能维护客户特性。");
    }

    private void EnsureCustomerFeatureVersion(Guid featureVersionId)
    {
        var version = featureVersions.List().SingleOrDefault(x => x.Id == featureVersionId) ?? throw new InvalidOperationException("许可证特性版本不存在。");
        if (version.Status != LmsFeatureVersionStatus.Active) throw new InvalidOperationException("停用的许可证特性版本不能用于客户特性。");
        if (version.Scope != LmsFeatureScope.Customer) throw new InvalidOperationException("仅客户范围的特性版本可以创建客户特性。");
    }
}
