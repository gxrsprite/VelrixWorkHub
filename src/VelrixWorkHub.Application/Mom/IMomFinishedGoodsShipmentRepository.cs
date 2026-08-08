using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomFinishedGoodsShipmentRepository
{
    IReadOnlyList<MomFinishedGoodsShipment> List();
    void Add(MomFinishedGoodsShipment item);
}

public interface IMomFinishedGoodsShipmentAllocationRepository
{
    IReadOnlyList<MomFinishedGoodsShipmentAllocation> List(Guid? shipmentId = null);
    void Add(MomFinishedGoodsShipmentAllocation item);
}
