namespace AdminBlazor;

public sealed class PasswordPolicyOptions
{
    public int MinimumLength { get; set; } = 7;
    public int MaximumLength { get; set; } = 128;
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireDigit { get; set; }
}

public sealed class PasswordPolicy
{
    private readonly PasswordPolicyOptions _options;

    public PasswordPolicy(PasswordPolicyOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumLength, options.MinimumLength);
        _options = options;
    }

    public string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "密码不能为空。";
        if (password.Length < _options.MinimumLength || password.Length > _options.MaximumLength)
            return $"密码长度必须在 {_options.MinimumLength} 到 {_options.MaximumLength} 个字符之间。";
        if (_options.RequireUppercase && !password.Any(char.IsUpper))
            return "密码必须包含大写字母。";
        if (_options.RequireLowercase && !password.Any(char.IsLower))
            return "密码必须包含小写字母。";
        if (_options.RequireDigit && !password.Any(char.IsDigit))
            return "密码必须包含数字。";

        return null;
    }
}
