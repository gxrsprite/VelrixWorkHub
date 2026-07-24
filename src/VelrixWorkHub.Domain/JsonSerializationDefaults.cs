using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VelrixWorkHub.Domain;

/// <summary>
/// 统一 JSON 枚举序列化约定，所有业务 JSON 使用枚举名称而不是底层数字。
/// </summary>
public static class JsonSerializationDefaults
{
    public static JsonSerializerOptions CreateWeb() => new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
            options.Converters.Add(new JsonStringEnumConverter());
    }
}
