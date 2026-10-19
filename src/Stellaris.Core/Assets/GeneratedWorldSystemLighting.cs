using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Assets;

public sealed record GeneratedWorldSystemLight(string StarClassId, string World, string LightName,
    IReadOnlyList<double> Color, double Intensity, IReadOnlyList<double> Position, double Radius, double Falloff)
{
    public string ConversionPolicy => "SourceLightThreeCalibrationV1";
}

public static class GeneratedWorldSystemLighting
{
    internal static IReadOnlyDictionary<string,string> Worlds { get; } = new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(StringComparer.Ordinal) {
        ["a_star"]="star_a_class.txt",["b_star"]="star_b_class.txt",["f_star"]="star_f_class.txt",["g_star"]="star_g_class.txt",
        ["k_star"]="star_k_class.txt",["m_star"]="star_m_class.txt",["t_star"]="star_t_class.txt",["neutron_star"]="star_neutron.txt",["pulsar"]="star_pulsar.txt",["black_hole"]="star_black_hole.txt" });
    public static IReadOnlyList<string> Paths { get; } = Array.AsReadOnly(new[]{"common/star_classes/00_star_classes.txt","gfx/lights/star_lights.asset"}
        .Concat(Worlds.Values.Select(file=>"gfx/worldgfx/"+file)).ToArray());

    /// <summary>Consumer проверяет actual system.StarClassId через этот immutable source map до world publish.</summary>
    public static async Task<IReadOnlyDictionary<string,GeneratedWorldSystemLight>> ResolveAsync(Func<string,CancellationToken,Task<byte[]>> read, CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(read);
        var roots=new Dictionary<string,ScriptBlock>(StringComparer.Ordinal); long sourceBytes=0;
        foreach(var path in Paths)
        {
            var bytes=await read(path,cancellationToken); sourceBytes+=bytes.Length;
            if(sourceBytes>16*1024*1024) throw new GeneratedWorldVisualException("definition-limit",null,"Lighting definitions суммарно >16MiB.");
            roots.Add(path,GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false,true).GetString(bytes),path));
        }
        return FromDefinitions(roots);
    }

    internal static IReadOnlyDictionary<string,GeneratedWorldSystemLight> FromDefinitions(IReadOnlyDictionary<string,ScriptBlock> roots)
    {
        try
        {
            var worlds=new Dictionary<string,GeneratedWorldSystemLight>(StringComparer.Ordinal);
            foreach(var (world,file) in Worlds)
            {
                var fields=Block(roots["gfx/worldgfx/"+file].Required("gfx_settings"));
                if(Scalar(fields,"world")!=world) throw new InvalidDataException("worldgfx tag mismatch.");
                var name=Scalar(fields,"system_light"); var candidates=roots["gfx/lights/star_lights.asset"].Named("light").Select(Block).Where(b=>Scalar(b,"name")==name).ToArray();
                if(candidates.Length!=1) throw new InvalidDataException("Missing/duplicate light.");
                var light=candidates[0]; var color=Block(light.Required("color"));
                var rgb=new[]{"r","g","b"}.Select(key=>
                {
                    var value=Block(color.Required(key));
                    if(value.Entries.Count!=1||value.Entries[0].Key!=""||value.Entries[0].Operator!="") throw new InvalidDataException("Invalid source RGB.");
                    return Number(value.Entries[0].AsScalar(),true);
                }).ToArray();
                var position=Block(light.Required("position")); var xyz=new[]{"x","y","z"}.Select(k=>Number(Scalar(position,k),false)).ToArray();
                worlds.Add(world,new("",world,name,Array.AsReadOnly(rgb),Number(Scalar(light,"intensity"),true),Array.AsReadOnly(xyz),Number(Scalar(light,"radius"),true),Number(Scalar(light,"falloff"),true)));
            }
            var result=new Dictionary<string,GeneratedWorldSystemLight>(StringComparer.Ordinal);
            foreach(var entry in roots["common/star_classes/00_star_classes.txt"].Entries.Where(e=>e.Key.StartsWith("sc_",StringComparison.Ordinal)))
            {
                var world=Scalar(Block(entry),"class");
                if(worlds.TryGetValue(world,out var light)) { if(!result.TryAdd(entry.Key,light with{StarClassId=entry.Key})) throw new InvalidDataException("Duplicate star class."); }
            }
            return new ReadOnlyDictionary<string,GeneratedWorldSystemLight>(result);
        }
        catch(Exception error) when(error is FormatException or InvalidDataException)
        {
            throw new GeneratedWorldVisualException("system-lighting",null,error.Message);
        }
    }
    private static ScriptBlock Block(ScriptEntry entry) { if(entry.Operator!="=") throw new InvalidDataException("Expected =."); return entry.AsBlock(); }
    private static string Scalar(ScriptBlock block,string key) { var entry=block.Required(key); if(entry.Operator!="=")throw new InvalidDataException("Expected =.");return entry.AsScalar(); }
    private static double Number(string text,bool nonnegative)
    {
        if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||nonnegative&&value<0) throw new InvalidDataException("Invalid source light number.");
        return value;
    }
}
