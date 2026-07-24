namespace VelrixWorkHub.Application.Lms;

public sealed record LmsCustomerReferenceImpact(
    int MachineReferenceCount,
    int CustomerFeatureReferenceCount,
    int MachineFeatureReferenceCount,
    int LicenseRequestReferenceCount,
    int AuthorizationReferenceCount)
{
    public bool HasReferences => MachineReferenceCount > 0 || CustomerFeatureReferenceCount > 0 || MachineFeatureReferenceCount > 0 || LicenseRequestReferenceCount > 0 || AuthorizationReferenceCount > 0;
}

/// <summary>提供给其他模块的 LMS 客户引用查询，不暴露 LMS 持久化实现。</summary>
public sealed class LmsCustomerReferenceService(
    ILmsCustomerMachineRepository machines,
    ILmsCustomerFeatureRepository customerFeatures,
    ILmsMachineFeatureRepository machineFeatures,
    ILmsLicenseRepository licenses)
{
    public LmsCustomerReferenceImpact GetImpact(Guid customerId)
    {
        var customerMachines = machines.List().Where(x => x.CustomerId == customerId).ToArray();
        var machineIds = customerMachines.Select(x => x.Id).ToHashSet();
        var requests = licenses.ListRequests();
        var authorizations = licenses.ListAuthorizations();
        return new LmsCustomerReferenceImpact(
            customerMachines.Length,
            customerFeatures.List().Count(x => x.CustomerId == customerId),
            machineFeatures.List().Count(x => machineIds.Contains(x.CustomerMachineId)),
            requests.Count(x => x.CustomerId == customerId),
            authorizations.Count(x => x.CustomerId == customerId));
    }
}
