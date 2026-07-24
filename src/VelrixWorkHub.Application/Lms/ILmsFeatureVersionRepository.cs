using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public interface ILmsFeatureVersionRepository
{
    IReadOnlyList<LmsFeatureVersion> List();
    void Add(LmsFeatureVersion item);
    void Update(LmsFeatureVersion item);
}
