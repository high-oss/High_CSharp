// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace High.OpenApi.Json;

/// <summary>
/// Serialises an enum using each member's <see cref="EnumMemberAttribute"/>
/// value — the exact string the spec declares (e.g. <c>"1D"</c>, <c>"STOP_LOSS"</c>)
/// — rather than the C# member name. Applied via
/// <c>[JsonConverter(typeof(SpecEnumConverter&lt;TEnum&gt;))]</c> on the enum
/// declaration itself, so every property of that type gets it automatically.
///
/// This is the mechanism behind the contract's rule that a spec enum is never
/// widened to a plain string in a hand-written model: the wire value is pinned
/// to the attribute, and a value the spec does not declare fails to parse
/// instead of round-tripping silently.
/// </summary>
public sealed class SpecEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> FromWire = new(StringComparer.Ordinal);
    private static readonly Dictionary<TEnum, string> ToWire = new();

    static SpecEnumConverter()
    {
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            var wire = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
            FromWire[wire] = value;
            ToWire[value] = wire;
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is not null && FromWire.TryGetValue(text, out var value)) return value;
        throw new JsonException(
            $"\"{text}\" is not a value {typeof(TEnum).Name} declares. Valid values: {string.Join(", ", FromWire.Keys)}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!ToWire.TryGetValue(value, out var wire))
        {
            throw new JsonException($"{value} is not a value {typeof(TEnum).Name} declares.");
        }
        writer.WriteStringValue(wire);
    }
}
