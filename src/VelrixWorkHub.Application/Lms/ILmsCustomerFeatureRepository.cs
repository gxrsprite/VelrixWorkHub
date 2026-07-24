using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public interface ILmsCustomerFeatureRepository
{
    IReadOnlyList<LmsCustomerFeature> List();
    void Add(LmsCustomerFeature item);
    void Update(LmsCustomerFeature item);
}
