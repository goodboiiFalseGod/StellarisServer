using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Stellaris.Core.Assets;

public sealed class AssetManifestException : Exception
{
    public AssetManifestException(string message) : base(message)
    {
    }
}

public sealed class AssetManifestFile
{
    public string RelativePath { get; }
    public long ByteLength { get; }
    public string Sha256 { get; }
    public string Kind { get; }

    public AssetManifestFile(string relativePath, long byteLength, string sha256, string kind)
    {
        AssetManifestCanonical.ValidateRelativePath(relativePath);
        if (byteLength < 0 || byteLength > M3AssetManifestBuilder.MAX_FILE_BYTES || sha256 is null || sha256.Length != 64 || sha256.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
        {
            throw new AssetManifestException($"Некорректные metadata: {relativePath}.");
        }
        if (kind is not ("asset" or "gfx" or "mesh" or "dds" or "txt") || !relativePath.EndsWith("." + kind, StringComparison.Ordinal))
        {
            throw new AssetManifestException($"Некорректный kind: {relativePath}.");
        }
        RelativePath = relativePath;
        ByteLength = byteLength;
        Sha256 = sha256;
        Kind = kind;
    }
}

public sealed class AssetManifestVisual
{
    public string VisualId { get; }
    public IReadOnlyList<string> Dependencies { get; }
    public string MaterialProfile { get; }

    internal AssetManifestVisual(string visualId, IEnumerable<string> dependencies, string materialProfile = "m3-static-galaxy-v4")
    {
        VisualId = visualId;
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        MaterialProfile = materialProfile;
    }
}

public sealed class M3AssetManifest
{
    public int SchemaVersion => 1;
    public string ProfileId => GeneratedRequirements?.ProfileId ?? M3AssetManifestBuilder.PROFILE_ID;
    public string ProfileDigest { get; }
    public string GameVersion => "4.5.2";
    public int ImporterVersion => GeneratedRequirements is null ? 9 : GeneratedRequirements.ObjectIds is null ? 10 : 11;
    public int MaterialProfileVersion => GeneratedRequirements is null ? 5 : 7;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeneratedWorldVisualRequirements? GeneratedRequirements { get; }
    public IReadOnlyList<AssetManifestFile> Files { get; }
    public IReadOnlyList<AssetManifestVisual> Visuals { get; }

    internal M3AssetManifest(IEnumerable<AssetManifestFile> files, GeneratedWorldVisualRequirements? generatedRequirements = null)
    {
        GeneratedRequirements = generatedRequirements;
        Files = new ReadOnlyCollection<AssetManifestFile>(files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
        ProfileDigest = AssetManifestCanonical.ComputeDigest(ProfileId, GameVersion, SchemaVersion, ImporterVersion, MaterialProfileVersion, Files);
        var ids = new[] { "ship.corvette.mammalian", "station.outpost.mammalian", "planet.continental", "star.g" }
            .Concat(generatedRequirements?.ClassIds.Select(generatedRequirements.VisualIdFor) ?? [])
            .Concat(generatedRequirements?.ObjectIds?.Select(id => "object." + id) ?? []);
        Visuals = Array.AsReadOnly(ids.Order(StringComparer.Ordinal).Select(id => new AssetManifestVisual(id, Files.Select(file => file.RelativePath),
            id == "world.pc_neutron_star" ? "generated-world-static-v1/SourceMip512V1/NeutronNoSurfaceAtmosphereMixV1/SourceClassCommonScaleV2/GeneratedMapUnitSurfaceV1" : id.StartsWith("world.", StringComparison.Ordinal) ? "generated-world-static-v1/SourceMip512V1/SourceClassCommonScaleV2/GeneratedMapUnitSurfaceV1" : id == "object.gateway_ruined" ? "generated-object-static-v1/SourceMip512V1/StaticGatewayBindPoseV1/GatewaySurfaceRadius6V1/GatewayOutsideOrbitalEnvelopeV1" : id == "object.natural_wormhole" ? "generated-object-static-v1/WormholeNativeMip0V1/StaticWormholeRingsV1/WormholeSurfaceRadius6V1/WormholeOutsideOrbitalEnvelopeV1" : generatedRequirements is not null ? "m3-static-galaxy-v4/SourceMip512V1" : "m3-static-galaxy-v4")).ToArray());
    }
}

public static class AssetManifestCanonical
{
    public static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath.Contains('\\') || relativePath.Any(character => char.IsControl(character) || character == ':') || relativePath.Split('/').Any(segment => segment is "" or "." or "..") || Path.IsPathRooted(relativePath))
        {
            // Не повторяем потенциально абсолютную пользовательскую строку в диагностике.
            throw new AssetManifestException("Недопустимый относительный путь ассета.");
        }
    }

    public static byte[] GetBytes(string profileId, string gameVersion, int schemaVersion, int importerVersion, int materialProfileVersion, IEnumerable<AssetManifestFile> files)
    {
        if (string.IsNullOrEmpty(profileId) || profileId.Any(char.IsControl) || string.IsNullOrEmpty(gameVersion) || gameVersion.Any(char.IsControl) || schemaVersion <= 0 || importerVersion <= 0 || materialProfileVersion <= 0 || files is null)
        {
            throw new AssetManifestException("Некорректный canonical header.");
        }
        var entries = files.ToArray();
        if (entries.Any(file => file is null) || entries.Select(file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new AssetManifestException("Повторные или отсутствующие canonical records.");
        }
        var text = new StringBuilder();
        text.Append(profileId).Append('\n').Append(gameVersion).Append('\n');
        text.Append(schemaVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append(importerVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append(materialProfileVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var file in entries.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            ValidateRelativePath(file.RelativePath);
            text.Append(file.RelativePath).Append('\t').Append(file.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(file.Sha256).Append('\n');
        }
        return new UTF8Encoding(false, true).GetBytes(text.ToString());
    }

    public static string ComputeDigest(string profileId, string gameVersion, int schemaVersion, int importerVersion, int materialProfileVersion, IEnumerable<AssetManifestFile> files)
    {
        return Convert.ToHexString(SHA256.HashData(GetBytes(profileId, gameVersion, schemaVersion, importerVersion, materialProfileVersion, files))).ToLowerInvariant();
    }
}
