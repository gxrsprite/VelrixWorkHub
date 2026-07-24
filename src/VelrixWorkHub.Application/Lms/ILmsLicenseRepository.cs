using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Lms;
public interface ILmsLicenseRepository
{
    IReadOnlyList<LmsLicenseRequest> ListRequests();
    IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations();
    IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycleEntries(Guid authorizationId);
    void Add(LmsLicenseRequest item); void Update(LmsLicenseRequest item); void RemoveRequest(Guid requestId);
    void Add(LmsLicenseAuthorization item); void Update(LmsLicenseAuthorization item);
    void Add(LmsLicenseLifecycleEntry item);
}
