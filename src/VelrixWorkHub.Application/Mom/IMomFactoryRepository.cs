using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomFactoryRepository
{
    IReadOnlyList<MomFactory> List();
    void Add(MomFactory item);
    void Update(MomFactory item);
}
