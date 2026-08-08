using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityReportRepository
{
    IReadOnlyList<MomQualityReport> List();
    void Add(MomQualityReport item);
    void Update(MomQualityReport item);
}
