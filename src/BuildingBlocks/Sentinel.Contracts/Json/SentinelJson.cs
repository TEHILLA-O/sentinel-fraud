using System.Text.Json;
using System.Text.Json.Serialization;
using Sentinel.Contracts.Events;

namespace Sentinel.Contracts.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(TransactionReceivedV1))]
[JsonSerializable(typeof(TransactionValidatedV1))]
[JsonSerializable(typeof(RiskDecisionV1))]
[JsonSerializable(typeof(TriggeredRuleV1))]
[JsonSerializable(typeof(AlertRaisedV1))]
[JsonSerializable(typeof(DeadLetterV1))]
[JsonSerializable(typeof(ProfileUpdatedV1))]
[JsonSerializable(typeof(CaseFeedbackV1))]
public partial class SentinelJsonContext : JsonSerializerContext;

public static class SentinelJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);
}
