namespace VelrixWorkHub.Application.Workflow;

/// <summary>同一业务对象的同一流程定义已存在运行实例。</summary>
public sealed class WorkflowRunningInstanceConflictException : InvalidOperationException
{
    public WorkflowRunningInstanceConflictException(Exception? innerException = null)
        : base("该业务对象已有运行中的流程实例。", innerException)
    {
    }
}
