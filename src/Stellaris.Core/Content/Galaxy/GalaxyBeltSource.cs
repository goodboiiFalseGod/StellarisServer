using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Galaxy;

namespace Stellaris.Core.Content.Galaxy;

/// <summary>Optional source registry; отсутствие/ошибки блокируют только активный пояс.</summary>
public sealed class GalaxyBeltSource
{
    public const string RelativePath = "common/asteroid_belts/00_asteroid_belts.txt";
    private readonly string root;
    private readonly GalaxySyntaxValue? syntax;
    private readonly string? error;
    public GalaxySourceFile? File { get; }
    public GalaxyBeltSource(string installationRoot)
    {
        root = Path.GetFullPath(installationRoot);
        var source = Read(); File = source.File;
        if (source.Bytes is null) { error = "Нет asteroid belts source."; return; }
        try { syntax = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(source.Bytes), RelativePath); }
        catch (Exception failure) when (failure is FormatException or DecoderFallbackException) { error = failure.Message; }
    }
    public GalaxyBeltMetadata Resolve(GalaxyBeltMetadata belt, string systemId, int ordinal)
    {
        GalaxyStructureException Invalid(string detail) => new("invalid-belt-source", belt.Type + ": " + detail, belt.Source);
        if (error is not null) throw Invalid(error);
        var entries = syntax!.Entries.Where(e => e.Key == belt.Type).ToArray();
        if (entries.Length != 1 || entries[0].Operator != "=" || entries[0].Value.Kind != GalaxySyntaxKind.Block)
            throw Invalid("Требуется unique type definition.");
        decimal Number(string key)
        {
            var values = entries[0].Value.Entries.Where(e => e.Key == key).ToArray();
            if (values.Length == 0) return 1; // Документированный source default.
            if (values.Length != 1 || values[0].Operator != "=") throw Invalid("Повторное поле " + key);
            if (values[0].Value.Kind != GalaxySyntaxKind.Scalar || !decimal.TryParse(values[0].Value.Text,
                System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
                throw Invalid("Требуется positive finite literal " + key);
            return value;
        }
        if (belt.Radius <= 0) throw Invalid("Требуется positive radius.");
        return belt with { Id = systemId + "/belt/" + ordinal.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Width = Number("width"), Density = Number("density") };
    }
    public void VerifyStable()
    { if (Read().File != File) throw new IOException("Asteroid belts source изменился после импорта."); }
    private (byte[]? Bytes, GalaxySourceFile? File) Read()
    {
        var path = Path.Combine(root, RelativePath);
        for (FileSystemInfo? part = new FileInfo(path); part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if (part.LinkTarget is not null || part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse asteroid belts path запрещён.");
        if (!System.IO.File.Exists(path)) return (null, null);
        var bytes = System.IO.File.ReadAllBytes(path);
        return (bytes, new(RelativePath, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
    }
}
