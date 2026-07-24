using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CashAdvanceRepaymentServiceTests
{
    [Fact]
    public void ApprovedRepaymentSettlesAdvanceAndSharesBalanceWithOffset()
    {
        var advances = new AdvanceRepository();
        var cashAdvanceService = new CashAdvanceService(advances, new OffsetRepository(), CreateExpenseService());
        var repayments = new RepaymentRepository();
        var service = new CashAdvanceRepaymentService(repayments, cashAdvanceService);
        var user = Guid.CreateVersion7();
        var advance = CreateApprovedAdvance(cashAdvanceService, advances, user, "JK-R-001", 500);

        advance.ApplyOffset(300);
        var repayment = service.Create(advance.Id, user, "alice", "交付部", "Velrix", "HK-001", "归还差旅借款", 200,
            DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.BankTransfer, "RT-001", "银行转账回单", "{}");
        repayment.Submit(DateTime.Now);
        service.ApplyApproval(repayment);

        Assert.Equal(OaCashAdvanceRepaymentStatus.Approved, repayment.Status);
        Assert.Equal(OaCashAdvanceStatus.Settled, advance.Status);
        Assert.Equal(500, advance.SettledAmount);
        Assert.Equal(0, advance.RemainingAmount);
    }

    [Fact]
    public void PendingRejectedAndCancelledRepaymentsDoNotSettleAdvance()
    {
        var advances = new AdvanceRepository();
        var cashAdvanceService = new CashAdvanceService(advances, new OffsetRepository(), CreateExpenseService());
        var repayments = new RepaymentRepository();
        var service = new CashAdvanceRepaymentService(repayments, cashAdvanceService);
        var user = Guid.CreateVersion7();
        var advance = CreateApprovedAdvance(cashAdvanceService, advances, user, "JK-R-002", 300);

        var pending = CreateRepayment(service, advance, user, "HK-002", 100);
        pending.Submit(DateTime.Now);
        var rejected = CreateRepayment(service, advance, user, "HK-003", 100);
        rejected.Submit(DateTime.Now);
        service.ApplyRejection(rejected, "凭据不完整");
        var cancelled = CreateRepayment(service, advance, user, "HK-004", 100);
        cancelled.Submit(DateTime.Now);
        service.Cancel(cancelled, user, "alice");

        Assert.Equal(0, advance.SettledAmount);
        Assert.Equal(OaCashAdvanceRepaymentStatus.Submitted, pending.Status);
        Assert.Equal(OaCashAdvanceRepaymentStatus.Rejected, rejected.Status);
        Assert.Equal(OaCashAdvanceRepaymentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void RepaymentEnforcesOwnerCurrentBalanceAndDocumentUniqueness()
    {
        var advances = new AdvanceRepository();
        var cashAdvanceService = new CashAdvanceService(advances, new OffsetRepository(), CreateExpenseService());
        var repayments = new RepaymentRepository();
        var service = new CashAdvanceRepaymentService(repayments, cashAdvanceService);
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var advance = CreateApprovedAdvance(cashAdvanceService, advances, user, "JK-R-003", 100);

        Assert.Throws<UnauthorizedAccessException>(() => service.Create(advance.Id, otherUser, "bob", "交付部", "Velrix", "HK-005", "越权", 10, DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.Cash, "R", "说明", "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(advance.Id, user, "alice", "交付部", "Velrix", "HK-006", "超额", 101, DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.Cash, "R", "说明", "{}"));

        var repayment = CreateRepayment(service, advance, user, "HK-007", 50);
        Assert.Throws<InvalidOperationException>(() => CreateRepayment(service, advance, user, "hk-007", 10));
        Assert.Throws<UnauthorizedAccessException>(() => service.Edit(repayment, otherUser, "alice", "交付部", "Velrix", "HK-007", "测试还款", 50, DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.Cash, "R", "说明", "{}"));
    }

    [Fact]
    public void ApprovalRechecksRemainingBalance()
    {
        var advances = new AdvanceRepository();
        var cashAdvanceService = new CashAdvanceService(advances, new OffsetRepository(), CreateExpenseService());
        var repayments = new RepaymentRepository();
        var service = new CashAdvanceRepaymentService(repayments, cashAdvanceService);
        var user = Guid.CreateVersion7();
        var advance = CreateApprovedAdvance(cashAdvanceService, advances, user, "JK-R-004", 100);
        var repayment = CreateRepayment(service, advance, user, "HK-008", 80);
        repayment.Submit(DateTime.Now);
        advance.ApplyOffset(30);

        Assert.Throws<InvalidOperationException>(() => service.ApplyApproval(repayment));
        Assert.Equal(OaCashAdvanceRepaymentStatus.Submitted, repayment.Status);
        Assert.Equal(30, advance.SettledAmount);
        Assert.Equal(70, advance.RemainingAmount);
    }

    [Fact]
    public void RejectedRepaymentCanBeEditedAndResubmittedByItsApplicant()
    {
        var advances = new AdvanceRepository();
        var cashAdvanceService = new CashAdvanceService(advances, new OffsetRepository(), CreateExpenseService());
        var repayments = new RepaymentRepository();
        var service = new CashAdvanceRepaymentService(repayments, cashAdvanceService);
        var user = Guid.CreateVersion7();
        var advance = CreateApprovedAdvance(cashAdvanceService, advances, user, "JK-R-005", 200);
        var repayment = CreateRepayment(service, advance, user, "HK-009", 100);
        repayment.Submit(DateTime.Now);
        service.ApplyRejection(repayment, "请补充回单");

        service.Edit(repayment, user, "alice", "交付部", "Velrix", "HK-009", "补充回单后重提", 120,
            DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.BankTransfer, "RT-009", "已补充银行回单", "{}");
        repayment.Submit(DateTime.Now);

        Assert.Equal(OaCashAdvanceRepaymentStatus.Submitted, repayment.Status);
        Assert.Equal("补充回单后重提", repayment.Title);
        Assert.Equal(120, repayment.Amount);
        Assert.Null(repayment.RejectionReason);
    }

    private static OaCashAdvanceRepayment CreateRepayment(CashAdvanceRepaymentService service, OaCashAdvance advance, Guid user, string documentNo, decimal amount)
        => service.Create(advance.Id, user, "alice", "交付部", "Velrix", documentNo, "测试还款", amount, DateOnly.FromDateTime(DateTime.Today), OaCashAdvanceRepaymentMethod.Cash, "R-001", "测试说明", "{}");

    private static OaCashAdvance CreateApprovedAdvance(CashAdvanceService service, AdvanceRepository repository, Guid user, string documentNo, decimal amount)
    {
        var item = service.Create(user, "alice", "交付部", "Velrix", documentNo, "测试借款", OaCashAdvanceType.Temporary,
            DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)), null, amount, "测试用途", "{}");
        item.Submit(DateTime.Now);
        item.Approve();
        repository.Update(item);
        return item;
    }

    private static ExpenseReimbursementService CreateExpenseService() => new(new ReimbursementRepository(), new LineRepository());

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

    private sealed class RepaymentRepository : IOaCashAdvanceRepaymentRepository
    {
        private readonly List<OaCashAdvanceRepayment> items = [];
        public IReadOnlyList<OaCashAdvanceRepayment> List(Guid? applicantUserId = null, Guid? cashAdvanceId = null) => items.Where(x => (applicantUserId is null || x.ApplicantUserId == applicantUserId) && (cashAdvanceId is null || x.CashAdvanceId == cashAdvanceId)).ToArray();
        public OaCashAdvanceRepayment? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaCashAdvanceRepayment item) => items.Add(item);
        public void Update(OaCashAdvanceRepayment item) { if (!items.Contains(item)) throw new InvalidOperationException(); }
    }

    private sealed class ReimbursementRepository : IOaExpenseReimbursementRepository
    {
        public IReadOnlyList<OaExpenseReimbursement> List(Guid? applicantUserId = null) => [];
        public OaExpenseReimbursement? Get(Guid id) => null;
        public void Add(OaExpenseReimbursement item) { }
        public void Update(OaExpenseReimbursement item) { }
    }

    private sealed class LineRepository : IOaExpenseLineRepository
    {
        public IReadOnlyList<OaExpenseLine> List(Guid? reimbursementId = null) => [];
        public OaExpenseLine? Get(Guid id) => null;
        public void Add(OaExpenseLine item) { }
        public void Update(OaExpenseLine item) { }
        public void Remove(Guid id) { }
    }
}
