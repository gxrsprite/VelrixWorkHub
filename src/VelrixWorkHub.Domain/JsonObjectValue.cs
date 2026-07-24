using System.Text.Json;

namespace VelrixWorkHub.Domain;

/// <summary>统一校验可扩展业务字段必须是 JSON 对象，并将空值归一化为对象。</summary>
public static class JsonObjectValue
{
    public static string Normalize(string? value, string parameterName)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("必须是 JSON 对象。", parameterName);
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("必须是有效 JSON。", parameterName, exception);
        }
    }
}
