using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Stellaris.Core.Assets;

/// <summary>Точный набор source classes мира. Не содержит путей или игровых bytes.</summary>
public sealed class GeneratedWorldVisualRequirements
{
    public const string POLICY = "SourceFirstNumberedEntityV1";
    public int Version => 1;
    public string Policy => POLICY;
    public IReadOnlyList<string> ClassIds { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ObjectIds { get; }
    [JsonIgnore] public string Fingerprint { get; }
    [JsonIgnore] public string ProfileId => M3AssetManifestBuilder.PROFILE_ID + (ObjectIds is null ? "-gmu1-gw1-" : "-gmo1-") + Fingerprint;

    public GeneratedWorldVisualRequirements(IEnumerable<string> classIds, IEnumerable<string>? objectIds = null)
    {
        ArgumentNullException.ThrowIfNull(classIds);
        var classes = classIds.Take(129).ToArray();
        if (classes.Length is < 1 or > 128 || classes.Any(id => !IsClassId(id)))
        {
            throw new GeneratedWorldVisualException("requirements", null, "Нужны 1..128 корректных source class IDs.");
        }
        ClassIds = Array.AsReadOnly(classes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        if (objectIds is not null)
        {
            var objects = objectIds.Take(17).ToArray();
            if (objects.Length is < 1 or > 16 || objects.Any(id => id is not ("gateway_ruined" or "natural_wormhole")) || objects.Distinct(StringComparer.Ordinal).Count() != objects.Length)
                throw new GeneratedWorldVisualException("object-requirements", null, "Разрешены уникальные bounded source objects gateway_ruined и natural_wormhole.");
            ObjectIds = Array.AsReadOnly(objects.Order(StringComparer.Ordinal).ToArray());
        }
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText()))).ToLowerInvariant();
    }

    public string CanonicalText() => "generated-world-visuals-v1\n" + POLICY + "\n" + string.Concat(ClassIds.Select(id => id + "\n")) +
        (ObjectIds is null ? string.Empty : "objects-v1\n" + string.Concat(ObjectIds.Select(id => id + "\n")));

    public string VisualIdFor(string classId)
    {
        if (!ClassIds.Contains(classId, StringComparer.Ordinal))
        {
            throw new GeneratedWorldVisualException("class-not-requested", classId, "Класс отсутствует в требованиях мира.");
        }
        return "world." + classId;
    }

    public static bool IsClassId(string? id) => id is not null && Regex.IsMatch(id, "\\Apc_[a-z0-9_]{1,96}\\z", RegexOptions.CultureInvariant);
}

public sealed class GeneratedWorldVisualException(string code, string? classId, string detail) : Exception($"Generated visual {classId}: {code}: {detail}")
{
    public string Code { get; } = code;
    public string? ClassId { get; } = classId;
}
