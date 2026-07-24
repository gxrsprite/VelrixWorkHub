using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public interface ILmsMachineFeatureRepository
{
    IReadOnlyList<LmsMachineFeature> List();
    void Add(LmsMachineFeature item);
    void Update(LmsMachineFeature item);
}
