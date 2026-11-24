using System.Text.Json;
using System.Text.Json.Serialization;

namespace SawooPlates.Core.Presets;

/// <summary>Stores a preset in JSON as its preset code.</summary>
public class PortraitPresetDataConverter : JsonConverter<PortraitPresetData>
{
    public override void Write(Utf8JsonWriter writer, PortraitPresetData value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.ToExportedString());

    public override PortraitPresetData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => PortraitPresetData.TryParse(reader.GetString(), out var preset) ? preset : null;
}
