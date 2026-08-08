using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityDispositionRepository
{
    IReadOnlyList<MomQualityDisposition> List();
    void Add(MomQualityDisposition item);
    void Update(MomQualityDisposition item);
}
