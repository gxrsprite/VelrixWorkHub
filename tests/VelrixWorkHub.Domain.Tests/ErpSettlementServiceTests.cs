using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpSettlementServiceTests
{
    [Fact]
    public void Payable_AllowsPartialSettlementAndCalculatesRemaining()
    {
        var supplier = Guid.NewGuid(); var product = Guid.NewGuid(); var order = new PurchaseOrder("PO-SET-01", supplier, product, DateOnly.FromDateTime(DateTime.Today), 2, 100m);
        var purchases = new PurchaseRepo(order); var sales = new SalesRepo(); var settlements = new SettlementRepo(); var service = new SettlementService(settlements, purchases, sales);
        service.Create(ErpSettlementKind.Payable, order.Id, 80m, "PAY-001", DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(120m, service.OrderBalances(ErpSettlementKind.Payable).Single().RemainingAmount);
    }

    [Fact]
    public void GetOrderBalance_IncludesSettledAndRemainingAmounts()
    {
        var order = new SalesOrder("SO-SET-BALANCE", Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 2, 100m);
        var service = new SettlementService(new SettlementRepo(), new PurchaseRepo(), new SalesRepo(order));
        service.Create(ErpSettlementKind.Receivable, order.Id, 60m, "REC-BALANCE-001", DateOnly.FromDateTime(DateTime.Today));

        var balance = service.GetOrderBalance(ErpSettlementKind.Receivable, order.Id);

        Assert.NotNull(balance);
        Assert.Equal(200m, balance.OrderAmount);
        Assert.Equal(60m, balance.SettledAmount);
        Assert.Equal(140m, balance.RemainingAmount);
    }

    [Fact]
    public void Settlement_RejectsOverpaymentAndDuplicateReference()
    {
        var order = new SalesOrder("SO-SET-01", Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 100m); var settlements = new SettlementRepo(); var service = new SettlementService(settlements, new PurchaseRepo(), new SalesRepo(order));
        service.Create(ErpSettlementKind.Receivable, order.Id, 100m, "REC-001", DateOnly.FromDateTime(DateTime.Today));
        Assert.Throws<InvalidOperationException>(() => service.Create(ErpSettlementKind.Receivable, order.Id, 1m, "REC-002", DateOnly.FromDateTime(DateTime.Today)));
        Assert.Throws<InvalidOperationException>(() => service.Create(ErpSettlementKind.Receivable, order.Id, 1m, "REC-001", DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public void Void_RestoresOrderBalanceAndPreservesSettlementHistory()
    {
        var order = new SalesOrder("SO-SET-VOID", Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 100m); var settlements = new SettlementRepo(); var service = new SettlementService(settlements, new PurchaseRepo(), new SalesRepo(order));
        var settlement = service.Create(ErpSettlementKind.Receivable, order.Id, 60m, "REC-VOID-001", DateOnly.FromDateTime(DateTime.Today));
        service.Void(settlement.Id, "银行回单录入错误");
        Assert.Equal(ErpSettlementStatus.Voided, service.List().Single().Status);
        Assert.Equal("银行回单录入错误", service.List().Single().VoidReason);
        Assert.Equal(100m, service.OrderBalances(ErpSettlementKind.Receivable).Single().RemainingAmount);
    }

    [Fact]
    public void List_FiltersByStatusAndReferenceOrVoidReason()
    {
        var order = new PurchaseOrder("PO-SET-FILTER", Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 100m); var settlements = new SettlementRepo(); var service = new SettlementService(settlements, new PurchaseRepo(order), new SalesRepo());
        service.Create(ErpSettlementKind.Payable, order.Id, 20m, "PAY-FILTER-01", DateOnly.FromDateTime(DateTime.Today), "采购付款");
        var voided = service.Create(ErpSettlementKind.Payable, order.Id, 10m, "PAY-FILTER-02", DateOnly.FromDateTime(DateTime.Today));
        service.Void(voided.Id, "回单错误");

        Assert.Single(service.List(status: ErpSettlementStatus.Active, keyword: "filter-01"));
        Assert.Single(service.List(status: ErpSettlementStatus.Voided, keyword: "回单"));
    }

    [Fact]
    public void List_FiltersByPartyIdWithoutMixingOtherCustomers()
    {
        var firstCustomer = Guid.NewGuid();
        var secondCustomer = Guid.NewGuid();
        var firstOrder = new SalesOrder("SO-PARTY-01", firstCustomer, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 100m);
        var secondOrder = new SalesOrder("SO-PARTY-02", secondCustomer, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 1, 200m);
        var service = new SettlementService(new SettlementRepo(), new PurchaseRepo(), new SalesRepo(firstOrder, secondOrder));

        service.Create(ErpSettlementKind.Receivable, firstOrder.Id, 30m, "REC-PARTY-01", DateOnly.FromDateTime(DateTime.Today));
        service.Create(ErpSettlementKind.Receivable, secondOrder.Id, 40m, "REC-PARTY-02", DateOnly.FromDateTime(DateTime.Today));

        var result = service.List(ErpSettlementKind.Receivable, partyId: firstCustomer);

        Assert.Single(result);
        Assert.Equal("REC-PARTY-01", result[0].ReferenceNo);
        Assert.Equal(firstCustomer, result[0].PartyId);
    }

    private sealed class SettlementRepo : ISettlementRepository { private readonly List<ErpSettlement> items = []; public IReadOnlyList<ErpSettlement> List() => items; public void Add(ErpSettlement item) => items.Add(item); public void Update(ErpSettlement item) { } }
    private sealed class PurchaseRepo(params PurchaseOrder[] items) : IPurchaseOrderRepository { private readonly List<PurchaseOrder> data = [.. items]; public IReadOnlyList<PurchaseOrder> List() => data; public void Add(PurchaseOrder item) => data.Add(item); public void Update(PurchaseOrder item) { } }
    private sealed class SalesRepo(params SalesOrder[] items) : ISalesOrderRepository { private readonly List<SalesOrder> data = [.. items]; public IReadOnlyList<SalesOrder> List() => data; public void Add(SalesOrder item) => data.Add(item); public void Update(SalesOrder item) { } }
}
