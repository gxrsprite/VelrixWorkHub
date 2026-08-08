using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomAcceptanceChecklistRepository
{
    IReadOnlyList<MomAcceptanceChecklistItem> List(Guid? acceptanceId = null);
    void Add(MomAcceptanceChecklistItem item);
    void Update(MomAcceptanceChecklistItem item);
    void Remove(Guid id);
}
