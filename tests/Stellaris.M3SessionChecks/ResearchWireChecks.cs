using System.Text.Json;
using System.Text.Json.Nodes;
using Stellaris.Session.M3;

internal static class ResearchWireChecks
{
    public static void Run()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var first = new M3ResearchUnavailableView("same-tech", "name", ["gate-a"], "key");
        var other = first with { Gates = ["gate-b"] };
        M3ResearchEmpireView Empire(int id, IReadOnlyList<M3ResearchUnavailableView>? unavailable) => new(id, id, [], [], unavailable);
        var original = new M3ResearchView([Empire(1, [first, first, other]), Empire(2, [other, first]), Empire(3, null), Empire(4, [])]);
        var json = JsonSerializer.Serialize(original, options);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetProperty("unavailableTable").GetArrayLength() != 2) throw new Exception("Таблица потеряла gates или не устранила повторы.");
        var roundtrip = JsonSerializer.Deserialize<M3ResearchView>(json, options)!;
        if (roundtrip.Empires.Count != original.Empires.Count) throw new Exception("Потеряны research empires.");
        for (var index = 0; index < original.Empires.Count; index++)
            if (JsonSerializer.Serialize(original.Empires[index], options) != JsonSerializer.Serialize(roundtrip.Empires[index], options)) throw new Exception("Упаковка изменила логический DTO.");
        var single = new M3ResearchView([Empire(1, [first])]);
        var legacy = JsonSerializer.Serialize(single, options);
        if (legacy.Contains("encoding", StringComparison.Ordinal) || JsonSerializer.Deserialize<M3ResearchView>(legacy, options)!.Empires.Single().Unavailable!.Count != 1) throw new Exception("Сломан обычный формат.");
        foreach (var invalid in new[] { "-1", "999999", "1.5", "\"0\"" })
        {
            var node = JsonNode.Parse(json)!;
            node["unavailableIndices"]![0]![0] = JsonNode.Parse(invalid);
            try { JsonSerializer.Deserialize<M3ResearchView>(node.ToJsonString(), options); }
            catch (JsonException) { continue; }
            throw new Exception("Неверная ссылка исследований принята.");
        }
    }
}
