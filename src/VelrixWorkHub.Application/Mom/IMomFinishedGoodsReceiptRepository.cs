using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomFinishedGoodsReceiptRepository
{
    IReadOnlyList<MomFinishedGoodsReceipt> List();
    void Add(MomFinishedGoodsReceipt item);
}
