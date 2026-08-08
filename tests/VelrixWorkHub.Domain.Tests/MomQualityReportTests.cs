using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomQualityReportTests
{
    [Fact]
    public void CompletedInspectionCreatesFrozenReportAndOnlyOneActiveReportIsAllowed()
    {
        var fixture = Fixture.Create(MomQualityInspectionStatus.Passed);
        var report = fixture.Service.CreateFromInspection(fixture.Inspection.Id, "quality-user");

        Assert.Equal(fixture.Inspection.Id, report.InspectionId);
        Assert.Equal(MomQualityReportStatus.Draft, report.Status);
        Assert.Equal("合格", report.Conclusion);
        Assert.Contains("Passed", report.SnapshotJson, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromInspection(fixture.Inspection.Id, "quality-user"));

        fixture.Service.Publish(report.Id, "quality-manager");
        Assert.Equal(MomQualityReportStatus.Published, report.Status);
        fixture.Service.Void(report.Id, "quality-manager");
        Assert.Equal(MomQualityReportStatus.Voided, report.Status);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromInspection(fixture.Inspection.Id, "quality-user"));
    }

    [Fact]
    public void PendingInspectionCannotCreateReport()
    {
        var fixture = Fixture.Create(null);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateFromInspection(fixture.Inspection.Id, "quality-user"));
    }

    [Fact]
    public void ReportPublishTransactionRestoresDraftState()
    {
        var fixture = Fixture.Create(MomQualityInspectionStatus.Passed);
        var report = fixture.Service.CreateFromInspection(fixture.Inspection.Id, "quality-user");
        var failing = new MomQualityReportService(fixture.Reports, fixture.Inspections, new ThrowingTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => failing.Publish(report.Id, "quality-manager"));
        Assert.Equal(MomQualityReportStatus.Draft, report.Status);
        Assert.Null(report.PublishedBy);
    }

    private sealed class Fixture
    {
        public MomQualityInspection Inspection { get; private init; } = null!;
        public InMemoryInspectionRepository Inspections { get; private init; } = null!;
        public InMemoryReportRepository Reports { get; private init; } = null!;
        public MomQualityReportService Service { get; private init; } = null!;

        public static Fixture Create(MomQualityInspectionStatus? status)
        {
            var inspection = new MomQualityInspection(Guid.CreateVersion7(), MomQualityInspectionType.Ipqc, Guid.CreateVersion7(), null, "BATCH-REPORT", null, 10, DateTime.Now);
            if (status is MomQualityInspectionStatus.Passed) inspection.RecordResult(10, 0, "inspector", DateTime.Now);
            if (status is MomQualityInspectionStatus.Failed) inspection.RecordResult(8, 2, "inspector", DateTime.Now);
            var inspections = new InMemoryInspectionRepository([inspection]);
            var reports = new InMemoryReportRepository();
            return new Fixture { Inspection = inspection, Inspections = inspections, Reports = reports, Service = new MomQualityReportService(reports, inspections) };
        }
    }

    private sealed class InMemoryInspectionRepository(IReadOnlyList<MomQualityInspection> seed) : IMomQualityInspectionRepository
    {
        private readonly List<MomQualityInspection> items = seed.ToList();
        public IReadOnlyList<MomQualityInspection> List() => items;
        public void Add(MomQualityInspection item) => items.Add(item);
        public void Update(MomQualityInspection item) { }
    }

    private sealed class InMemoryReportRepository : IMomQualityReportRepository
    {
        private readonly List<MomQualityReport> items = [];
        public IReadOnlyList<MomQualityReport> List() => items;
        public void Add(MomQualityReport item) => items.Add(item);
        public void Update(MomQualityReport item) { }
    }

    private sealed class ThrowingTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            operation();
            var exception = new InvalidOperationException("模拟质量报告事务失败。");
            afterRollback?.Invoke(exception);
            throw exception;
        }
    }
}
