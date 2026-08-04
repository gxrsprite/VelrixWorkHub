using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PurchaseOrders;
public sealed class PurchaseOrderService(IPurchaseOrderRepository repository, ISupplierRepository supplierRepository, IProductRepository productRepository, IInventoryTransactionRepository inventoryRepository, IWarehouseRepository warehouseRepository, ISettlementRepository settlementRepository, WorkflowApprovalService? approval = null, IOaProcurementRequestRepository? procurementRequests = null, ProcurementBudgetService? procurementBudgets = null, IWorkflowTransactionBoundary? transactions = null, InventoryService? inventoryService = null) : IPurchaseOrderWorkflowApprover
{
    public IReadOnlyList<PurchaseOrder> List(string? keyword = null, PurchaseOrderSourceKind? sourceKind = null) { var query = repository.List().AsEnumerable(); var text = keyword?.Trim(); if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.OrderNo.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.SourceDocumentNo?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)); if (sourceKind is PurchaseOrderSourceKind kind) query = query.Where(x => x.SourceKind == kind); return query.OrderByDescending(x => x.OrderDate).ToArray(); }
    public PurchaseOrder Create(string orderNo, Guid supplierId, Guid productId, DateOnly date, decimal quantity, decimal unitPrice, PurchaseOrderSourceKind sourceKind = PurchaseOrderSourceKind.Manual, string? sourceDocumentNo = null, DateOnly? dueDate = null, Guid? sourceLineId = null) { var product = EnsureReferences(supplierId, productId); var item = new PurchaseOrder(orderNo, supplierId, productId, date, quantity, unitPrice, sourceKind, sourceDocumentNo, dueDate, sourceLineId); var existing = repository.List(); if (product.MaxPurchaseQuantity is decimal max && quantity > max) throw new InvalidOperationException($"采购数量不能超过商品单次最大采购量 {max:N2}。"); if (existing.Any(x => x.OrderNo.Equals(item.OrderNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("采购订单号已存在。"); if (item.SourceKind != PurchaseOrderSourceKind.Manual && existing.Any(x => x.Status != PurchaseOrderStatus.Cancelled && x.SourceKind == item.SourceKind && string.Equals(x.SourceDocumentNo, item.SourceDocumentNo, StringComparison.OrdinalIgnoreCase) && x.SourceLineId == item.SourceLineId)) throw new InvalidOperationException(item.SourceLineId is null ? "来源单据已生成采购订单，不能重复生单；如需重试请先取消原采购订单。" : "来源明细已生成采购订单，不能重复生单；如需重试请先取消原采购订单。"); repository.Add(item); return item; }
    public void SetLocked(PurchaseOrder item, bool locked) { item.SetLocked(locked); repository.Update(item); }
    public void SetStatus(PurchaseOrder item, PurchaseOrderStatus status) { if (status == PurchaseOrderStatus.Submitted && item.Status == PurchaseOrderStatus.Draft) approval?.RequireCompleted(WorkflowBindingCodes.PurchaseOrderApproval, nameof(PurchaseOrder), item.Id, "采购订单提交"); EnsureCanChangeStatus(item, status); var previousStatus = item.Status; void Core() { if (status == PurchaseOrderStatus.Cancelled && item.SourceKind == PurchaseOrderSourceKind.Requisition && !string.IsNullOrWhiteSpace(item.SourceDocumentNo)) { var request = procurementRequests?.List().SingleOrDefault(x => x.DocumentNo.Equals(item.SourceDocumentNo, StringComparison.OrdinalIgnoreCase)); if (request is not null) procurementBudgets?.ReleaseForCancelledOrder(request); } item.SetStatus(status); repository.Update(item); } if (transactions is null) Core(); else transactions.Execute(Core, _ => item.SetStatus(previousStatus)); }
    public void ApplyApproval(PurchaseOrder item)
    {
        if (item.Status == PurchaseOrderStatus.Submitted) return;
        if (item.Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException($"采购订单不能从“{item.Status}”通过审批。");
        item.SetStatus(PurchaseOrderStatus.Submitted);
        repository.Update(item);
    }
    public void Receive(PurchaseOrder item, Guid? warehouseId = null, Guid? locationId = null)
    {
        if (item.Status != PurchaseOrderStatus.Submitted) throw new InvalidOperationException("只有已提交的采购订单可以收货。");
        var warehouses = warehouseRepository.List();
        var warehouse = warehouseId is Guid selectedWarehouseId
            ? warehouses.FirstOrDefault(x => x.Id == selectedWarehouseId) ?? throw new InvalidOperationException("收货仓库不存在。")
            : warehouses.FirstOrDefault(x => x.Status == WarehouseStatus.Active) ?? throw new InvalidOperationException("没有可用的启用仓库。");
        if (warehouse.Status != WarehouseStatus.Active) throw new InvalidOperationException("收货仓库已停用。");
        if (locationId is not null && !warehouse.Locations.Any(x => x.Id == locationId)) throw new InvalidOperationException("收货库位不属于所选仓库。");
        var sourceNo = $"{item.OrderNo}-IN";
        if (inventoryRepository.List().Any(x => x.SourceNo.Equals(sourceNo, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("该采购订单已生成入库流水。");
        var previousStatus = item.Status;
        void Core()
        {
            item.SetStatus(PurchaseOrderStatus.Received);
            repository.Update(item);
            if (inventoryService is null) inventoryRepository.Add(new InventoryTransaction(item.ProductId, warehouse.Id, InventoryTransactionKind.Inbound, item.Quantity, sourceNo, item.OrderDate, $"采购订单 {item.OrderNo} 收货入库", locationId));
            else inventoryService.Create(item.ProductId, warehouse.Id, InventoryTransactionKind.Inbound, item.Quantity, sourceNo, item.OrderDate, $"采购订单 {item.OrderNo} 收货入库", locationId);
        }

        if (transactions is null) Core();
        else transactions.Execute(Core, _ => item.SetStatusForRecovery(previousStatus));
    }
    private Product EnsureReferences(Guid supplierId, Guid productId) { var supplier = supplierRepository.List().FirstOrDefault(x => x.Id == supplierId); if (supplier is null) throw new InvalidOperationException("供应商不存在。"); if (supplier.Status != SupplierStatus.Active) throw new InvalidOperationException("供应商已停用，不能创建采购订单。"); if (supplier.QualificationStatus != SupplierQualificationStatus.Qualified) throw new InvalidOperationException("供应商未通过采购准入，不能创建采购订单。"); var product = productRepository.List().FirstOrDefault(x => x.Id == productId); if (product is null) throw new InvalidOperationException("商品不存在。"); if (product.Status != ProductStatus.Active) throw new InvalidOperationException("商品已停用，不能创建采购订单。"); return product; }
    private void EnsureCanChangeStatus(PurchaseOrder item, PurchaseOrderStatus status) { if (status == PurchaseOrderStatus.Cancelled) { approval?.RequireNotRunning(WorkflowBindingCodes.PurchaseOrderApproval, nameof(PurchaseOrder), item.Id, "取消采购订单"); if (settlementRepository.List().Any(x => x.OrderId == item.Id && x.Kind == ErpSettlementKind.Payable && x.Status == ErpSettlementStatus.Active)) throw new InvalidOperationException("采购订单已有有效付款核销，不能取消订单；请先撤销核销。"); } }
}
