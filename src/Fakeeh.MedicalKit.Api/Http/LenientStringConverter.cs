using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fakeeh.MedicalKit.Api.Http;

/// <summary>
/// Reads JSON numbers into string properties as well as strings, so the KIT can send
/// ids such as nationalId, nurseId or deviceId either as 1123456781 or "1123456781".
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string>
{
    public override bool HandleNull => false;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan),
            _ => throw new JsonException($"Expected a string or a number but found {reader.TokenType}.")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
