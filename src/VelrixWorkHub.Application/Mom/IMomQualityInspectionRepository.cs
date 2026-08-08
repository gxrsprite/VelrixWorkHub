using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityInspectionRepository
{
    IReadOnlyList<MomQualityInspection> List();
    void Add(MomQualityInspection item);
    void Update(MomQualityInspection item);
}
