using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityInspectionStandardRepository
{
    IReadOnlyList<MomQualityInspectionStandard> List();
    void Add(MomQualityInspectionStandard item);
    void Update(MomQualityInspectionStandard item);
}
