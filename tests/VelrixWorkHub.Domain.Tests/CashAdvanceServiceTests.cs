using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Application.ExpenseReimbursements;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CashAdvanceServiceTests
{
    [Fact]
    public void ApprovedAdvanceCanBeOffsetBySameApplicantsApprovedReimbursement()
    {
        var advances = new AdvanceRepository();
        var offsets = new OffsetRepository();
        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var expenseService = new ExpenseReimbursementService(reimbursements, lines);
        var service = new CashAdvanceService(advances, offsets, expenseService);
        var user = Guid.CreateVersion7();
        var advance = Create(service, user, "JK-001", 500);
        advance.Submit(DateTime.Now);
        advance.Approve();
        advances.Update(advance);
        var reimbursement = expenseService.Create(user, "alice", "交付部", "Velrix 上海有限公司", "BX-001", "差旅报销", DateOnly.FromDateTime(DateTime.Today), OaExpenseReimbursementType.Travel, null, false, false, false, "出差", "{}");
        expenseService.AddLine(reimbursement, user, "交通费", "高铁", "INV-001", null, DateOnly.FromDateTime(DateTime.Today), 300, 300, null, "{}");
        expenseService.Submit(reimbursement, user);
        reimbursement.Approve();
        reimbursements.Update(reimbursement);

        service.ApplyOffset(advance, user, reimbursement.Id, 300, DateOnly.FromDateTime(DateTime.Today), "差旅报销冲销", "{\"source\":\"travel\"}");

        Assert.Equal(OaCashAdvanceStatus.PartiallySettled, advance.Status);
        Assert.Equal(200, advance.RemainingAmount);
        Assert.Single(offsets.List(advance.Id));
        Assert.Equal("{\"source\":\"travel\"}", offsets.List(advance.Id).Single().OtherInfo);

        Assert.Throws<InvalidOperationException>(() => service.ApplyOffset(advance, user, reimbursement.Id, 100, DateOnly.FromDateTime(DateTime.Today), "重复冲销", "{}"));
        service.ApplyOffset(advance, user, CreateApprovedReimbursement(expenseService, reimbursements, user, "BX-002", "INV-002", amount: 200).Id, 200, DateOnly.FromDateTime(DateTime.Today), "完成冲销", "{}");
        Assert.Equal(OaCashAdvanceStatus.Settled, advance.Status);
        Assert.Equal(0, advance.RemainingAmount);
    }

    [Fact]
    public void OffsetRejectsDifferentApplicantOrUnapprovedReimbursement()
    {
        var advances = new AdvanceRepository();
        var offsets = new OffsetRepository();
        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var expenseService = new ExpenseReimbursementService(reimbursements, lines);
        var service = new CashAdvanceService(advances, offsets, expenseService);
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var advance = Create(service, user, "JK-003", 100);
        advance.Submit(DateTime.Now);
        advance.Approve();
        advances.Update(advance);
        var otherReimbursement = CreateApprovedReimbursement(expenseService, reimbursements, otherUser, "BX-003", "INV-003", approved: false);
        var unapprovedReimbursement = CreateApprovedReimbursement(expenseService, reimbursements, user, "BX-004", "INV-004", approved: false);

        Assert.Throws<UnauthorizedAccessException>(() => service.ApplyOffset(advance, otherUser, otherReimbursement.Id, 50, DateOnly.FromDateTime(DateTime.Today), "越权", "{}"));
        Assert.Throws<InvalidOperationException>(() => service.ApplyOffset(advance, user, unapprovedReimbursement.Id, 50, DateOnly.FromDateTime(DateTime.Today), "未批准", "{}"));
    }

    [Fact]
    public void RejectedAdvanceCanBeEditedAndResubmitted()
    {
        var advances = new AdvanceRepository();
        var service = new CashAdvanceService(advances, new OffsetRepository(), new ExpenseReimbursementService(new ReimbursementRepository(), new LineRepository()));
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "JK-004", 80);
        item.Submit(DateTime.Now);
        item.Reject("用途不清");
        advances.Update(item);
        service.Edit(item, user, "alice", "交付部", "Velrix 上海有限公司", item.DocumentNo, item.Title, item.AdvanceType, item.RequestDate, item.ExpectedSettlementDate, null, 100, "补充客户现场备用金用途", "{}");
        service.Submit(item, user);

        Assert.Equal(OaCashAdvanceStatus.Submitted, item.Status);
        Assert.Equal(100, item.Amount);
        Assert.Null(item.RejectionReason);
    }

    [Fact]
    public void InvalidDateAndOtherInfoAreRejected()
    {
        var user = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new OaCashAdvance(user, "alice", "交付部", "Velrix", "JK-005", "测试", OaCashAdvanceType.Other, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 19), null, 1, "用途", "{}", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaCashAdvance(user, "alice", "交付部", "Velrix", "JK-006", "测试", OaCashAdvanceType.Other, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), null, 1, "用途", "[]", DateTime.Now));
    }

    private static OaCashAdvance Create(CashAdvanceService service, Guid user, string documentNo, decimal amount)
        => service.Create(user, "alice", "交付部", "Velrix 上海有限公司", documentNo, "测试借款", OaCashAdvanceType.Temporary, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)), null, amount, "测试用途", "{}");

    private static OaExpenseReimbursement CreateApprovedReimbursement(ExpenseReimbursementService service, ReimbursementRepository repository, Guid user, string documentNo, string invoiceNo, bool approved = true, decimal amount = 100)
    {
        var item = service.Create(user, "alice", "交付部", "Velrix 上海有限公司", documentNo, "测试报销", DateOnly.FromDateTime(DateTime.Today), OaExpenseReimbursementType.General, null, false, false, false, "测试事由", "{}");
        service.AddLine(item, user, "办公费", "测试费用", invoiceNo, null, DateOnly.FromDateTime(DateTime.Today), amount, amount, null, "{}");
        service.Submit(item, user);
        if (approved) item.Approve();
        repository.Update(item);
        return item;
    }

    private sealed class AdvanceRepository : IOaCashAdvanceRepository
    {
        private readonly List<OaCashAdvance> items = [];
        public IReadOnlyList<OaCashAdvance> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaCashAdvance? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaCashAdvance item) => items.Add(item);
        public void Update(OaCashAdvance item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
    }

    private sealed class OffsetRepository : IOaCashAdvanceOffsetRepository
    {
        private readonly List<OaCashAdvanceOffset> items = [];
        public IReadOnlyList<OaCashAdvanceOffset> List(Guid? cashAdvanceId = null) => items.Where(x => cashAdvanceId is null || x.CashAdvanceId == cashAdvanceId).ToArray();
        public void Add(OaCashAdvanceOffset item) => items.Add(item);
    }

    private sealed class ReimbursementRepository : IOaExpenseReimbursementRepository
    {
        private readonly List<OaExpenseReimbursement> items = [];
        public IReadOnlyList<OaExpenseReimbursement> List(Guid? applicantUserId = null) => items.Where(x => applicantUserId is null || x.ApplicantUserId == applicantUserId).ToArray();
        public OaExpenseReimbursement? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaExpenseReimbursement item) => items.Add(item);
        public void Update(OaExpenseReimbursement item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
    }

    private sealed class LineRepository : IOaExpenseLineRepository
    {
        private readonly List<OaExpenseLine> items = [];
        public IReadOnlyList<OaExpenseLine> List(Guid? reimbursementId = null) => items.Where(x => reimbursementId is null || x.ReimbursementId == reimbursementId).ToArray();
        public OaExpenseLine? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaExpenseLine item) => items.Add(item);
        public void Update(OaExpenseLine item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }
}
