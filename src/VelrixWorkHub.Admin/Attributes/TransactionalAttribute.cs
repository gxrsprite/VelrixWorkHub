namespace AdminBlazor;

/// <summary>
/// 事务标记 — 用于标识需要在事务中执行的方法
/// 实际事务管理由 AdminTable2 的 SaveAsync / RemoveAsync 中的 FreeSql UnitOfWork 处理
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TransactionalAttribute : Attribute
{
}
