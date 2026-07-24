using VelrixWorkHub.Application.ExpenseReimbursements;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ExpenseReimbursementServiceTests
{
    [Fact]
    public void LinesRecalculateActualAmountAndRejectDuplicateInvoiceAcrossDocuments()
    {
        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var service = new ExpenseReimbursementService(reimbursements, lines);
        var user = Guid.CreateVersion7();
        var first = Create(service, user, "BX-001");

        service.AddLine(first, user, "交通费", "高铁", "INV-001", null, DateOnly.FromDateTime(DateTime.Today), 120, 100, null, "{\"source\":\"客户拜访\"}");

        Assert.Equal(100, first.ActualAmount);
        Assert.Equal("{\"source\":\"客户拜访\"}", lines.List(first.Id).Single().OtherInfo);

        var second = Create(service, user, "BX-002");
        var exception = Assert.Throws<InvalidOperationException>(() => service.AddLine(second, user, "住宿费", "酒店", "INV-001", null, DateOnly.FromDateTime(DateTime.Today), 300, 300, null, "{}"));

        Assert.Contains("流水号", exception.Message);
        Assert.Empty(lines.List(second.Id));
    }

    [Fact]
    public void SubmitRequiresLineAndOnlyOwnerCanOperate()
    {
        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var service = new ExpenseReimbursementService(reimbursements, lines);
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var item = Create(service, user, "BX-003");

        Assert.Throws<InvalidOperationException>(() => service.Submit(item, user));
        Assert.Throws<UnauthorizedAccessException>(() => service.AddLine(item, otherUser, "餐费", "工作餐", null, null, DateOnly.FromDateTime(DateTime.Today), 50, 50, null, "{}"));

        service.AddLine(item, user, "餐费", "工作餐", null, null, DateOnly.FromDateTime(DateTime.Today), 50, 50, null, "{}");
        service.Submit(item, user);

        Assert.Equal(OaExpenseReimbursementStatus.Submitted, item.Status);
        Assert.Throws<UnauthorizedAccessException>(() => service.Cancel(item, otherUser, "other"));
    }

    [Fact]
    public void RejectedReimbursementCanBeSubmittedAgainAfterEditing()
    {
        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var service = new ExpenseReimbursementService(reimbursements, lines);
        var user = Guid.CreateVersion7();
        var item = Create(service, user, "BX-004");
        service.AddLine(item, user, "办公费", "文具", "INV-004", null, DateOnly.FromDateTime(DateTime.Today), 80, 80, null, "{}");
        service.Submit(item, user);
        item.Reject("请补充用途");
        reimbursements.Update(item);

        service.Edit(item, user, item.ApplicantName, item.DepartmentName, item.LegalEntity, item.DocumentNo, item.Title,
            item.ReimbursementDate, item.ReimbursementType, item.ProjectId, item.IsEntrusted, item.IsTeamBuilding, item.IsEntertainment,
            "补充后的费用用途", item.OtherInfo);
        service.Submit(item, user);

        Assert.Equal(OaExpenseReimbursementStatus.Submitted, item.Status);
        Assert.Null(item.RejectionReason);
    }

    [Fact]
    public void InvalidOtherInfoAndInvalidAmountAreRejected()
    {
        var user = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => Create(new ExpenseReimbursementService(new ReimbursementRepository(), new LineRepository()), user, "BX-005", "not-json"));

        var reimbursements = new ReimbursementRepository();
        var lines = new LineRepository();
        var service = new ExpenseReimbursementService(reimbursements, lines);
        var item = Create(service, user, "BX-006");

        Assert.Throws<ArgumentOutOfRangeException>(() => service.AddLine(item, user, "办公费", "无效金额", null, null, DateOnly.FromDateTime(DateTime.Today), 10, 11, null, "{}"));
    }

    private static OaExpenseReimbursement Create(ExpenseReimbursementService service, Guid user, string documentNo, string otherInfo = "{}")
        => service.Create(user, "alice", "交付部", "Velrix 上海有限公司", documentNo, "测试报销", DateOnly.FromDateTime(DateTime.Today), OaExpenseReimbursementType.General, null, false, false, false, "测试事由", otherInfo);

    private sealed class ReimbursementRepository(params OaExpenseReimbursement[] seed) : IOaExpenseReimbursementRepository
    {
        private readonly List<OaExpenseReimbursement> items = [.. seed];
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
