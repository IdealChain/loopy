using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loopy.Comm.Extensions;

/// <summary>
/// Supports reading numeric JSON values into string fields,
/// and writes number-ish strings out again as numeric JSON values
/// </summary>
public sealed class IntegerAutoConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan),
            _ => reader.GetString(),
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (long.TryParse(value, out var number))
            writer.WriteNumberValue(number);
        else
            writer.WriteStringValue(value);
    }
}
