using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaris.Session.M3;

namespace Stellaris.Server;

public sealed record M3JoinMessage(string Name, string Role, string? Token, string JoinCode);
public sealed record M3InboundMessage(bool IsPing, M3ClientCommand? Command);

public static class M3WireCodec
{
    public const int MAX_INCOMING_BYTES = 16 * 1024;
    public const int MAX_OUTGOING_BYTES = 1024 * 1024;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private static readonly IReadOnlyDictionary<string, string[]> _fields = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["assetReady"] = ["profileId", "profileDigest", "ready", "galaxyWorldFingerprint"],
        ["configure"] = ["traits", "buildings"],
        ["selectEmpireTemplate"] = ["templateId"],
        ["setGalaxySettings"] = ["galaxySeed", "galaxyScenarioId", "galaxyShapeId", "galaxyDensity", "galaxyNebulae", "galaxyGuaranteedColonies"],
        ["prepareGalaxy"] = [],
        ["inspectGalaxySystem"] = ["systemId", "galaxyWorldFingerprint"],
        ["selectResearch"] = ["area", "technologyId", "expectedResearchRevision"],
        ["enqueueConstruction"] = ["colonyId", "expectedColonyRevision", "definitionId", "slotId", "orderId"],
        ["enqueueUpgrade"] = ["colonyId", "expectedColonyRevision", "sourceDefinitionId", "definitionId", "slotId", "zoneInstanceId", "orderId"],
        ["enqueueZoneReplacement"] = ["colonyId", "expectedColonyRevision", "sourceDefinitionId", "definitionId", "slotId", "zoneInstanceId", "orderId", "zoneReplacementPreviewId", "confirmZoneRemoval"],
        ["setBuildingEnabled"] = ["colonyId", "expectedColonyRevision", "definitionId", "slotId", "enabled"],
        ["demolishBuilding"] = ["colonyId", "expectedColonyRevision", "definitionId", "slotId", "confirmDemolition"],
        ["clearBlocker"] = ["colonyId", "expectedColonyRevision", "definitionId", "slotId", "orderId"],
        ["cancelConstruction"] = ["colonyId", "expectedColonyRevision", "orderId", "refundPreviewId", "confirmRefundLoss"],
        ["reorderConstruction"] = ["colonyId", "expectedColonyRevision", "orderIds"],
        ["ready"] = ["ready", "expectedLobbyRevision"],
        ["start"] = ["expectedLobbyRevision", "profileId", "profileDigest", "galaxyWorldFingerprint"],
        ["setJobs"] = ["planetId", "job", "count"],
        ["adjustWorkforceLimit"] = ["colonyId", "expectedColonyRevision", "job", "count"],
        ["setWorkforceLimitMin"] = ["colonyId", "expectedColonyRevision", "job"],
        ["setWorkforceLimitMax"] = ["colonyId", "expectedColonyRevision", "job"],
        ["setWorkforceLimit"] = ["colonyId", "expectedColonyRevision", "job", "count"],
        ["pause"] = ["paused"],
        ["speed"] = ["speed"],
        ["stepDay"] = [],
        ["step"] = [],
        ["transferHost"] = ["targetParticipantId"],
        ["kickParticipant"] = ["targetParticipantId"],
        ["approveObserver"] = ["targetParticipantId"],
        ["observerView"] = ["targetParticipantId"],
        ["moveFleetToPoint"] = ["fleetId", "systemId", "x", "z"],
        ["moveFleetToSystem"] = ["fleetId", "targetSystemId"],
        ["stopFleet"] = ["fleetId"]
    };

    public static M3JoinMessage ParseJoin(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes);
        var root = document.RootElement;
        ValidateFields(root, ["type", "protocol", "name", "role", "token", "joinCode"]);
        if (ReadString(root, "type") != "join" || !root.TryGetProperty("protocol", out var protocol) || protocol.ValueKind != JsonValueKind.Number || !protocol.TryGetInt32(out var version) || version != 2)
        {
            throw new InvalidDataException("Нужен join protocol=2.");
        }
        string? token = null;
        if (root.TryGetProperty("token", out var rawToken) && rawToken.ValueKind != JsonValueKind.Null)
        {
            token = ReadString(root, "token");
        }
        return new M3JoinMessage(ReadString(root, "name"), ReadString(root, "role"), token, ReadString(root, "joinCode"));
    }

    public static M3InboundMessage ParseMessage(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes);
        var root = document.RootElement;
        var type = ReadString(root, "type");
        if (type == "ping")
        {
            ValidateFields(root, ["type"]);
            return new M3InboundMessage(true, null);
        }
        var fields = _fields.GetValueOrDefault(type, []);
        ValidateFields(root, ["type", "commandId", .. fields]);
        var commandId = ReadString(root, "commandId");
        if (commandId.Length is < 1 or > 64)
        {
            throw new InvalidDataException("commandId должен содержать 1..64 символа.");
        }
        foreach (var field in fields)
        {
            if (!root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }
            var valid = field switch
            {
                "ready" or "paused" or "confirmRefundLoss" or "enabled" or "confirmDemolition" or "confirmZoneRemoval" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "planetId" or "speed" or "targetParticipantId" or "galaxyNebulae" or "galaxyGuaranteedColonies" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                "expectedColonyRevision" or "expectedResearchRevision" or "expectedLobbyRevision" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "count" or "galaxyDensity" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
                "x" or "z" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
                "traits" or "buildings" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
                "orderIds" => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0
                    && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && item.GetString() is { } id && !string.IsNullOrWhiteSpace(id) && id.Length <= 160 && !id.Any(char.IsControl))
                    && value.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count() == value.GetArrayLength(),
                _ => value.ValueKind == JsonValueKind.String
            };
            if (!valid)
            {
                throw new InvalidDataException("Неверный тип аргумента команды.");
            }
        }
        if ((type is "enqueueUpgrade" or "enqueueZoneReplacement" or "reorderConstruction" or "setBuildingEnabled" or "demolishBuilding" or "kickParticipant") && fields.Any(field => !root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null))
        { throw new InvalidDataException("Отсутствует обязательное поле команды."); }
        var command = root.Deserialize<M3ClientCommand>(_json) ?? throw new InvalidDataException("Пустая команда.");
        return new M3InboundMessage(false, command);
    }

    public static byte[] Serialize(object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _json);
        ValidateOutgoing(bytes);
        return bytes;
    }

    public static void ValidateOutgoing(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MAX_OUTGOING_BYTES)
        {
            throw new InvalidDataException("Исходящее сообщение превышает 1 MiB.");
        }
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MAX_INCOMING_BYTES)
        {
            throw new InvalidDataException("Входящее сообщение превышает 16 KiB.");
        }
        try
        {
            var text = _utf8.GetString(bytes.Span);
            var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new InvalidDataException("JSON должен быть объектом.");
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in document.RootElement.EnumerateObject())
            {
                if (!names.Add(field.Name))
                {
                    document.Dispose();
                    throw new InvalidDataException("Повторное JSON-поле.");
                }
            }
            return document;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or JsonException)
        {
            throw new InvalidDataException("Неверный UTF-8/JSON.");
        }
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Отсутствует обязательное строковое поле.");
        }
        return value.GetString()!;
    }

    private static void ValidateFields(JsonElement root, IReadOnlyCollection<string> allowed)
    {
        if (root.EnumerateObject().Any(field => !allowed.Contains(field.Name, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Неизвестное либо постороннее поле команды.");
        }
    }
}

public sealed class M3Outbox : IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<byte[]> _replies = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private byte[]? _snapshot;
    private bool _complete;

    public int PendingReplies
    {
        get
        {
            lock (_gate)
            {
                return _replies.Count;
            }
        }
    }

    public void EnqueueReply(byte[] bytes)
    {
        M3WireCodec.ValidateOutgoing(bytes);
        lock (_gate)
        {
            if (_complete) return;
            if (_replies.Count >= 64)
            {
                throw new InvalidDataException("Клиент не успевает получать ответы.");
            }
            _replies.Enqueue((byte[])bytes.Clone());
            Wake();
        }
    }

    public void ReplaceSnapshot(byte[] bytes)
    {
        M3WireCodec.ValidateOutgoing(bytes);
        lock (_gate)
        {
            if (_complete) return;
            _snapshot = (byte[])bytes.Clone();
            Wake();
        }
    }

    public void CompleteWithFinalReply(byte[] bytes)
    {
        M3WireCodec.ValidateOutgoing(bytes);
        lock (_gate)
        {
            if (_complete) return;
            _replies.Clear();
            _snapshot = null;
            _replies.Enqueue((byte[])bytes.Clone());
            _complete = true;
            Wake();
        }
    }

    public async Task<byte[]?> TakeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_replies.Count > 0)
                {
                    return _replies.Dequeue();
                }
                if (_snapshot is not null)
                {
                    var snapshot = _snapshot;
                    _snapshot = null;
                    return snapshot;
                }
                if (_complete)
                {
                    return null;
                }
            }
            await _signal.WaitAsync(cancellationToken);
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _complete = true;
            Wake();
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
    }

    private void Wake()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }
}

public sealed class M3RateLimiter
{
    private readonly Queue<TimeSpan> _messages = new();
    private TimeSpan _last;

    public bool Accept(TimeSpan now)
    {
        if (now < _last)
        {
            throw new ArgumentException("Время rate limiter должно быть монотонным.");
        }
        _last = now;
        while (_messages.TryPeek(out var first) && now - first >= TimeSpan.FromSeconds(1))
        {
            _messages.Dequeue();
        }
        if (_messages.Count >= 20)
        {
            return false;
        }
        _messages.Enqueue(now);
        return true;
    }
}
