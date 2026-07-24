using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public class WorkTaskTests
{
    [Fact]
    public void NewTask_StartsAsTodo()
    {
        var task = new WorkTask("整理客户跟进资料");

        Assert.Equal(WorkTaskStatus.Todo, task.Status);
    }

    [Fact]
    public void CompletedTask_CanBeReopenedButCannotStartDirectly()
    {
        var task = new WorkTask("准备周会");
        task.Complete();

        Assert.Throws<InvalidOperationException>(() => task.Start());

        task.Reopen();
        task.Start();
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Edit_TrimsValuesAndAllowsClearingOptionalFields()
    {
        var task = new WorkTask("旧标题", "旧备注", DateOnly.FromDateTime(DateTime.Today));

        task.Edit(" 新标题 ", "  ", null);

        Assert.Equal("新标题", task.Title);
        Assert.Null(task.Description);
        Assert.Null(task.DueDate);
    }

    [Fact]
    public void Edit_RejectsBlankTitle()
    {
        var task = new WorkTask("保留标题");

        Assert.Throws<ArgumentException>(() => task.Edit(" ", null, null));
        Assert.Equal("保留标题", task.Title);
    }
}
