using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsMachineFeatureService(
    ILmsMachineFeatureRepository repository,
    LmsCustomerMachineService machines,
    LmsCustomerFeatureService customerFeatures,
    LmsFeatureVersionService featureVersions)
{
    public IReadOnlyList<LmsMachineFeature> List(Guid? customerMachineId = null, bool includeDisabled = true) =>
        repository.List()
            .Where(x => customerMachineId is null || x.CustomerMachineId == customerMachineId)
            .Where(x => includeDisabled || x.Status == LmsMachineFeatureStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();

    public LmsMachineFeature Create(Guid customerMachineId, Guid featureVersionId, DateTime? expiresAt, string? notes, string? otherInfo)
    {
        var machine = EnsureMachine(customerMachineId);
        var version = EnsureMachineFeatureVersion(featureVersionId);
        EnsureWithinCustomerBaseline(machine.CustomerId, version);
        if (repository.List().Any(x => x.CustomerMachineId == customerMachineId && SameFeature(x.FeatureVersionId, version.FeatureId)))
        {
            throw new InvalidOperationException("该机台已存在同一特性的授权或限制记录。");
        }
        var item = new LmsMachineFeature(customerMachineId, featureVersionId, expiresAt, notes, otherInfo, DateTime.Now);
        repository.Add(item);
        return item;
    }

    public void Edit(LmsMachineFeature item, DateTime? expiresAt, string? notes, string? otherInfo)
    {
        var machine = EnsureMachine(item.CustomerMachineId);
        var version = EnsureMachineFeatureVersion(item.FeatureVersionId);
        EnsureWithinCustomerBaseline(machine.CustomerId, version);
        item.Edit(expiresAt, notes, otherInfo);
        repository.Update(item);
    }

    public void SetStatus(LmsMachineFeature item, LmsMachineFeatureStatus status)
    {
        item.SetStatus(status);
        repository.Update(item);
    }

    private LmsCustomerMachine EnsureMachine(Guid customerMachineId)
    {
        var machine = machines.List().SingleOrDefault(x => x.Id == customerMachineId) ?? throw new InvalidOperationException("客户机台不存在。");
        if (machine.Status != LmsCustomerMachineStatus.Active) throw new InvalidOperationException("停用的客户机台不能维护机台特性。");
        return machine;
    }

    private LmsFeatureVersion EnsureMachineFeatureVersion(Guid featureVersionId)
    {
        var version = featureVersions.List().SingleOrDefault(x => x.Id == featureVersionId) ?? throw new InvalidOperationException("许可证特性版本不存在。");
        if (version.Status != LmsFeatureVersionStatus.Active) throw new InvalidOperationException("停用的许可证特性版本不能用于机台特性。");
        if (version.Scope != LmsFeatureScope.Machine) throw new InvalidOperationException("仅机台范围的特性版本可以创建机台特性。");
        return version;
    }

    private void EnsureWithinCustomerBaseline(Guid customerId, LmsFeatureVersion machineVersion)
    {
        var now = DateTime.Now;
        var versions = featureVersions.List();
        var baseline = customerFeatures.List(customerId, includeDisabled: false)
            .Select(item => new { Item = item, Version = versions.SingleOrDefault(x => x.Id == item.FeatureVersionId) })
            .FirstOrDefault(x => x.Version is not null
                && x.Version.FeatureId == machineVersion.FeatureId
                && (x.Item.ExpiresAt is null || x.Item.ExpiresAt >= now));
        if (baseline?.Version is null)
        {
            throw new InvalidOperationException("该客户没有同一特性的有效客户授权基线。");
        }
        if (baseline.Version.Level < machineVersion.Level)
        {
            throw new InvalidOperationException("机台特性等级不能超过客户授权基线。");
        }
    }

    private bool SameFeature(Guid featureVersionId, Guid featureId) =>
        featureVersions.List().SingleOrDefault(x => x.Id == featureVersionId)?.FeatureId == featureId;
}
