namespace AdminBlazor;

/// <summary>
/// 按钮权限标记 — 注明方法需要的按钮权限 (button path)
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class AdminButtonAttribute : Attribute
{
    public string ButtonPath { get; }
    public AdminButtonAttribute(string buttonPath) { ButtonPath = buttonPath; }
}
