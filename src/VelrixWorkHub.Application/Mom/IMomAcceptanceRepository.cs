using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomAcceptanceRepository
{
    IReadOnlyList<MomAcceptance> List();
    void Add(MomAcceptance item);
    void Update(MomAcceptance item);
}
