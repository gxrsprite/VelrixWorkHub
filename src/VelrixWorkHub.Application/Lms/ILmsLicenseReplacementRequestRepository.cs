using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public interface ILmsLicenseReplacementRequestRepository
{
    IReadOnlyList<LmsLicenseReplacementRequest> List();
    void Add(LmsLicenseReplacementRequest item);
    void Update(LmsLicenseReplacementRequest item);
}
