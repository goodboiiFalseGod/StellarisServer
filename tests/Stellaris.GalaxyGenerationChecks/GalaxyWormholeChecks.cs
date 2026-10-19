using System.Text.RegularExpressions;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;

internal static class GalaxyWormholeChecks
{
    public static void RunFocused(string root, string worker, string workerSha)
    {
        CheckPure();
        CheckSources(root);
        var catalog=GalaxyLobbyCatalog.Import(Path.GetFullPath(root),new([],[]));
        var backend=new NativeTriangleBackend(Path.GetFullPath(worker),workerSha,
            Path.Combine(Path.GetTempPath(),"wormhole-triangle-"+Guid.NewGuid().ToString("N")),TimeSpan.FromSeconds(30));
        var request=new GalaxyLobbyRequest("wormhole-focused","tiny","elliptical",1,
            [new(1,"economic","pc_continental")],specialContent:new(WormholePairs:1));
        var resolved=catalog.Resolve(request);
        if(!resolved.Success) throw new Exception(resolved.Failure!.ToString());
        var generated=new GalaxyWorldGenerator(catalog,backend).Generate(resolved.Request!);
        if(!generated.Success) throw new Exception(generated.Failure!.ToString());
        var world=generated.World!;
        var requested=world.Stages.Single(item=>item.Stage=="wormhole-attempts-requested").Count;
        var created=world.Stages.Single(item=>item.Stage=="wormhole-pairs-created").Count;
        if(requested!=5||created!=world.Wormholes.Count/2||world.Wormholes.Count%2!=0||
            world.Wormholes.GroupBy(item=>item.PairId,StringComparer.Ordinal).Any(group=>group.Count()!=2)||
            world.Wormholes.Select(item=>item.SystemId).Distinct(StringComparer.Ordinal).Count()!=world.Wormholes.Count)
            throw new Exception("Focused runtime wormhole attempts/pairs/uniqueness mismatch.");
        var systems=world.Systems.ToDictionary(item=>item.Id,StringComparer.Ordinal);
        foreach(var item in world.Wormholes)
        {
            if(!systems.TryGetValue(item.SystemId,out var system)||system.Flags.Contains("starting_system",StringComparer.Ordinal)||system.Flags.Contains("empire_cluster",StringComparer.Ordinal)||
                item.PlacementPolicy!="WormholeOutsideOrbitalEnvelopeV1"||Math.Sqrt(item.X*item.X+item.Z*item.Z)+1e-6<40)
                throw new Exception("Focused runtime wormhole endpoint escaped its bounded placement/system rules.");
        }
        Console.WriteLine($"PASS wormhole tiny runtime systems={world.Systems.Count} requestedAttempts={requested} createdPairs={created} endpoints={world.Wormholes.Count}");
    }

    private static void CheckPure()
    {
        var systems=new[]
        {
            System("system/0000",0,0),System("system/0001",50_000_000,0),System("system/0002",99_999_999,0),
            System("system/0003",150_000_000,0),System("system/0004",250_000_000,0,["starting_system"]),
            System("system/0005",350_000_000,0,["empire_cluster"])
        };
        var bodies=new[]
        {
            new GalaxyWorldBody("body/0000","system/0000",null,GalaxyBodyKind.Planet,"pc_test",20,0,0,20,0,null,[]),
            new GalaxyWorldBody("body/0002","system/0002",null,GalaxyBodyKind.Planet,"pc_test",20,0,0,100,0,null,[])
        };
        var gateways=new[]
        {
            new GalaxyWorldGateway("gateway/system/0000","system/0000",10,0,0,"gateway_ruined","test",6,16),
            new GalaxyWorldGateway("gateway/system/0001","system/0001",80,0,0,"gateway_ruined","test",6,86)
        };
        var expectedEnvelopes=new Dictionary<string,double>(StringComparer.Ordinal)
        { ["system/0000"]=30,["system/0001"]=86,["system/0002"]=110,["system/0003"]=0 };
        if(!GalaxyWormholeGeneration.DistanceAtLeast500(new(0,0),new(50_000_000,0))||
            GalaxyWormholeGeneration.DistanceAtLeast500(new(0,0),new(49_999_999,0)))
            throw new Exception("Wormhole distance threshold must be inclusive at exactly 500 local map units.");
        var zeroSelection=NativeRandom.FromSpecifiedSeed(31);var zeroPlacement=NativeRandom.FromSpecifiedSeed(37);long zeroSelectionDraws=0,zeroPlacementDraws=0;
        var zero=GalaxyWormholeGeneration.Generate(systems,bodies,gateways,new HashSet<string>(StringComparer.Ordinal){"system/0003"},0,
            ref zeroSelection,ref zeroPlacement,ref zeroSelectionDraws,ref zeroPlacementDraws);
        if(zero.Count!=0||zeroSelectionDraws!=0||zeroPlacementDraws!=0||zeroSelection!=NativeRandom.FromSpecifiedSeed(31)||zeroPlacement!=NativeRandom.FromSpecifiedSeed(37))
            throw new Exception("Wormhole factor 0 must not draw from either domain.");
        IReadOnlyList<GalaxyWorldWormhole> Generate(int factor,out long selectionDraws,out long placementDraws)
        {
            var selection=NativeRandom.FromSpecifiedSeed(43);var placement=NativeRandom.FromSpecifiedSeed(47);selectionDraws=0;placementDraws=0;
            return GalaxyWormholeGeneration.Generate(systems,bodies,gateways,new HashSet<string>(StringComparer.Ordinal){"system/0003"},factor,
                ref selection,ref placement,ref selectionDraws,ref placementDraws);
        }
        foreach(var factor in new[]{1,5})
        {
            var first=Generate(factor,out var selectionDraws,out var placementDraws);var replay=Generate(factor,out var replaySelectionDraws,out var replayPlacementDraws);
            if(!first.SequenceEqual(replay)||selectionDraws!=replaySelectionDraws||placementDraws!=replayPlacementDraws||
                first.Count<2||first.Count%2!=0||first.Count/2>factor*5||first.GroupBy(item=>item.PairId,StringComparer.Ordinal).Any(group=>group.Count()!=2)||
                first.Select(item=>item.SystemId).Distinct(StringComparer.Ordinal).Count()!=first.Count||
                first.Any(item=>item.SystemId is "system/0003" or "system/0004" or "system/0005"||
                    !expectedEnvelopes.TryGetValue(item.SystemId,out var envelope)||Math.Abs(item.EnvelopeRadiusProxy-envelope)>1e-6||
                    Math.Sqrt(item.X*item.X+item.Z*item.Z)+1e-6<envelope+40))
                throw new Exception($"Wormhole factor {factor} deterministic pairing/exclusions/placement failed.");
        }
        Console.WriteLine("PASS wormhole factor, separate no-draw zero, inclusive 500 boundary, deterministic symmetric pairs and exclusions");
        static GalaxyWorldSystem System(string id,long x,long y,IReadOnlyList<string>? flags=null)=>new(id,0,new(x,y),0,"test","sc_test",null,flags??[],[],[]);
    }

    private static void CheckSources(string root)
    {
        var source=new GalaxyWormholeSource(Path.GetFullPath(root));source.Require();source.VerifyStable();
        var fixture=Path.Combine(Path.GetTempPath(),"wormhole-source-"+Guid.NewGuid().ToString("N"));
        try
        {
            foreach(var path in new[]{GalaxyWormholeSource.EventPath,GalaxyWormholeSource.OnActionPath,GalaxyWormholeSource.BypassPath})
            {
                var destination=Path.Combine(fixture,path);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(Path.Combine(root,path),destination);
            }
            var eventPath=Path.Combine(fixture,GalaxyWormholeSource.EventPath);var text=File.ReadAllText(eventPath);
            var changed=Regex.Replace(text,@"(?s)(event\s*=\s*\{\s*id\s*=\s*game_start\.31.*?while\s*=\s*\{\s*count\s*=\s*local_wormholes_num)","$1 extra_active_field = yes",RegexOptions.None,TimeSpan.FromSeconds(2));
            if(changed==text) throw new Exception("Could not locate source event body for bounded negative fixture.");
            File.WriteAllText(eventPath,changed);
            var malformed=new GalaxyWormholeSource(fixture);
            try { malformed.Require(); }
            catch(GalaxyStructureException) { Console.WriteLine("PASS wormhole source rejects an extra active while field");return; }
            throw new Exception("Wormhole source validator accepted an extra active source field.");
        }
        finally { if(Directory.Exists(fixture))Directory.Delete(fixture,true); }
    }
}
