using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityNonconformanceRepository
{
    IReadOnlyList<MomQualityNonconformance> List();
    void Add(MomQualityNonconformance item);
    void Update(MomQualityNonconformance item);
}
