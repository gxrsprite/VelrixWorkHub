using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkCenterRepository
{
    IReadOnlyList<MomWorkCenter> List();
    void Add(MomWorkCenter item);
    void Update(MomWorkCenter item);
}
