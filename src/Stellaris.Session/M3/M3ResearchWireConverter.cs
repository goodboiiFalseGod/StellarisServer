using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stellaris.Session.M3;

// Таблица строится только из уже отфильтрованной проекции. Логические DTO не меняются.
public sealed class M3ResearchWireConverter : JsonConverter<M3ResearchView>
{
    public const string ENCODING = "shared-unavailable-v1";
    public override void Write(Utf8JsonWriter writer, M3ResearchView value, JsonSerializerOptions options)
    {
        if (value.Empires.Count < 2)
        {
            JsonSerializer.Serialize(writer, new { value.Empires }, options);
            return;
        }
        var table = new List<M3ResearchUnavailableView>();
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var indices = value.Empires.Select(empire => empire.Unavailable?.Select(item => {
            var key = JsonSerializer.Serialize(item, options);
            if (!keys.TryGetValue(key, out var index)) { index = table.Count; table.Add(item); keys.Add(key, index); }
            return index;
        }).ToArray()).ToArray();
        writer.WriteStartObject();
        writer.WriteString(Name("Encoding", options), ENCODING);
        writer.WritePropertyName(Name("Empires", options));
        JsonSerializer.Serialize(writer, value.Empires.Select(empire => empire with { Unavailable = null }).ToArray(), options);
        writer.WritePropertyName(Name("UnavailableTable", options));
        JsonSerializer.Serialize(writer, table, options);
        writer.WritePropertyName(Name("UnavailableIndices", options));
        JsonSerializer.Serialize(writer, indices, options);
        writer.WriteEndObject();
    }

    public override M3ResearchView Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var empires = root.GetProperty(Name("Empires", options)).Deserialize<M3ResearchEmpireView[]>(options) ?? throw new JsonException("Нет research empires.");
        if (!root.TryGetProperty(Name("Encoding", options), out var encoding)) return new(empires);
        if (encoding.GetString() != ENCODING) throw new JsonException("Неизвестная таблица исследований.");
        var table = root.GetProperty(Name("UnavailableTable", options)).Deserialize<M3ResearchUnavailableView[]>(options) ?? throw new JsonException("Нет research table.");
        var strictIndices = new JsonSerializerOptions(options) { NumberHandling = JsonNumberHandling.Strict };
        var indices = root.GetProperty(Name("UnavailableIndices", options)).Deserialize<int[]?[]>(strictIndices) ?? throw new JsonException("Нет research indices.");
        if (indices.Length != empires.Length || table.Any(item => item is null)) throw new JsonException("Неверная research table.");
        return new(Array.AsReadOnly(empires.Select((empire, index) => empire with {
            Unavailable = indices[index] is not { } references ? null : Array.AsReadOnly(references.Select(reference =>
                reference >= 0 && reference < table.Length ? table[reference] : throw new JsonException("Индекс исследований вне таблицы.")).ToArray())
        }).ToArray()));
    }

    private static string Name(string name, JsonSerializerOptions options) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
}
