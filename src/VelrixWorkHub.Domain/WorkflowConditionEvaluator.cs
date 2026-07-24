using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VelrixWorkHub.Domain;

/// <summary>
/// 对流程条件节点执行受限表达式求值。表达式只读取调用方提供的字段上下文，不能执行脚本或访问业务对象。
/// </summary>
public static class WorkflowConditionEvaluator
{
    private static readonly Regex Comparison = new(
        "^(?<field>[A-Za-z_][A-Za-z0-9_.]*)\\s*(?<operator>==|!=|>=|<=|>|<|contains|startsWith|endsWith|is)\\s*(?<literal>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? SelectBranch(string configJson, IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        using var document = JsonDocument.Parse(configJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("条件节点配置必须是 JSON 对象。", nameof(configJson));
        if (root.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in branches.EnumerateArray())
            {
                var key = ReadRequiredString(branch, "key");
                var expression = ReadRequiredString(branch, "expression");
                if (Evaluate(expression, fields)) return key;
            }

            return ReadOptionalString(root, "defaultKey");
        }

        var legacyExpression = ReadRequiredString(root, "expression");
        return Evaluate(legacyExpression, fields)
            ? ReadOptionalString(root, "trueKey") ?? "true"
            : ReadOptionalString(root, "falseKey") ?? "false";
    }

    public static IReadOnlyList<string> GetBranchKeys(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("条件节点配置必须是 JSON 对象。", nameof(configJson));
        if (root.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array)
            return branches.EnumerateArray().Select(x => ReadRequiredString(x, "key")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return [ReadOptionalString(root, "trueKey") ?? "true", ReadOptionalString(root, "falseKey") ?? "false"];
    }

    public static void Validate(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("条件节点配置必须是 JSON 对象。", nameof(configJson));
        if (root.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var branch in branches.EnumerateArray())
            {
                var key = ReadRequiredString(branch, "key");
                if (!keys.Add(key)) throw new ArgumentException($"条件分支键重复：{key}。", nameof(configJson));
                _ = Evaluate(ReadRequiredString(branch, "expression"), new Dictionary<string, object?>());
            }

            if (branches.GetArrayLength() == 0) throw new ArgumentException("条件节点至少需要一个可求值分支。", nameof(configJson));
            var defaultKey = ReadOptionalString(root, "defaultKey");
            if (defaultKey is not null && !keys.Contains(defaultKey)) throw new ArgumentException("条件默认分支键必须存在于 branches。", nameof(configJson));
            return;
        }

        _ = Evaluate(ReadRequiredString(root, "expression"), new Dictionary<string, object?>());
    }

    public static bool Evaluate(string expression, IReadOnlyDictionary<string, object?> fields)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("条件表达式不能为空。", nameof(expression));
        ArgumentNullException.ThrowIfNull(fields);
        var orParts = expression.Split("||", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (orParts.Length == 0) throw new ArgumentException("条件表达式不能为空。", nameof(expression));
        return orParts.Any(andPart => andPart.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).All(part => EvaluateComparison(part, fields)));
    }

    private static bool EvaluateComparison(string expression, IReadOnlyDictionary<string, object?> fields)
    {
        var match = Comparison.Match(expression.Trim());
        if (!match.Success) throw new ArgumentException($"不支持的条件表达式：{expression}。", nameof(expression));
        var field = match.Groups["field"].Value;
        var operation = match.Groups["operator"].Value;
        var literal = ParseLiteral(match.Groups["literal"].Value.Trim());
        var actual = fields.FirstOrDefault(x => x.Key.Equals(field, StringComparison.OrdinalIgnoreCase)).Value;
        if (actual is JsonElement actualJson) actual = ConvertJsonValue(actualJson);

        return operation.ToLowerInvariant() switch
        {
            "==" => AreEqual(actual, literal),
            "!=" => !AreEqual(actual, literal),
            ">" => actual is not null && literal is not null && Compare(actual, literal) > 0,
            ">=" => actual is not null && literal is not null && Compare(actual, literal) >= 0,
            "<" => actual is not null && literal is not null && Compare(actual, literal) < 0,
            "<=" => actual is not null && literal is not null && Compare(actual, literal) <= 0,
            "contains" => actual is not null && literal is not null && ConvertToText(actual).Contains(ConvertToText(literal), StringComparison.OrdinalIgnoreCase),
            "startswith" => actual is not null && literal is not null && ConvertToText(actual).StartsWith(ConvertToText(literal), StringComparison.OrdinalIgnoreCase),
            "endswith" => actual is not null && literal is not null && ConvertToText(actual).EndsWith(ConvertToText(literal), StringComparison.OrdinalIgnoreCase),
            "is" => AreEqual(actual, literal),
            _ => throw new ArgumentException($"不支持的条件运算符：{operation}。", nameof(expression))
        };
    }

    private static object? ParseLiteral(string literal)
    {
        if (literal.Length >= 2 && ((literal[0] == '"' && literal[^1] == '"') || (literal[0] == '\'' && literal[^1] == '\'')))
            return literal[1..^1];
        if (bool.TryParse(literal, out var boolean)) return boolean;
        if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase)) return null;
        if (decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return number;
        return literal;
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is JsonElement leftJson) left = ConvertJsonValue(leftJson);
        if (right is JsonElement rightJson) right = ConvertJsonValue(rightJson);
        if (left is null || right is null) return left is null && right is null;
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber)) return leftNumber == rightNumber;
        if (left is bool leftBoolean && right is bool rightBoolean) return leftBoolean == rightBoolean;
        return string.Equals(ConvertToText(left), ConvertToText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? left, object? right)
    {
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber)) return leftNumber.CompareTo(rightNumber);
        return string.Compare(ConvertToText(left), ConvertToText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecimal(object? value, out decimal result)
    {
        if (value is JsonElement json) value = ConvertJsonValue(json);
        return decimal.TryParse(ConvertToText(value), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static object? ConvertJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.String => value.GetString(),
        _ => value.ToString()
    };

    private static string ConvertToText(object? value) => value switch
    {
        null => string.Empty,
        JsonElement json => ConvertToText(ConvertJsonValue(json)),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        var value = ReadOptionalString(element, propertyName);
        return value ?? throw new ArgumentException($"条件配置缺少 {propertyName}。", nameof(propertyName));
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
