using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Galaxy;
using Stellaris.Session.M3;

var sessionOnly = args.Length == 4 && args[3] == "--session-only";
var nebulaOnly = args.Length == 4 && args[3] == "--nebula-only";
var namesBudgetOnly = args.Length == 4 && args[3] == "--names-budget-only";
var guaranteedOnly = args.Length == 4 && args[3] == "--guaranteed-only";
var sizesOnly = args.Length == 4 && args[3] == "--sizes-only";
var beltsOnly = args.Length == 4 && args[3] == "--belts-only";
var ringsOnly = args.Length == 4 && args[3] == "--rings-only";
var miscOnly = args.Length == 4 && args[3] == "--misc-only";
var projectionOnly = args.Length == 4 && args[3] == "--projection-only";
var gatewayOnly = args.Length == 4 && args[3] == "--gateway-only";
var habitableOnly = args.Length == 4 && args[3] == "--habitable-only";
var wormholeOnly = args.Length == 4 && args[3] == "--wormhole-only";
var navigationOnly = args.Length == 4 && args[3] == "--navigation-only";
var playersOnly = args.Length == 4 && args[3] == "--players-only";
if (args.Take(3).ToArray() is not [var root, var worker, var sha] || args.Length != (sessionOnly || playersOnly || nebulaOnly || namesBudgetOnly || guaranteedOnly || sizesOnly || beltsOnly || ringsOnly || miscOnly || projectionOnly || gatewayOnly || navigationOnly || habitableOnly || wormholeOnly ? 4 : 3)) { Console.Error.WriteLine("game-root triangle-worker worker-sha256 [--session-only|--players-only|--nebula-only|--names-budget-only|--guaranteed-only|--sizes-only|--belts-only|--rings-only|--misc-only|--projection-only|--gateway-only|--navigation-only|--habitable-only|--wormhole-only]"); return 2; }
var source = GalaxyLobbyCatalog.Import(root, new([], []));
var backend = new NativeTriangleBackend(Path.GetFullPath(worker), sha, Path.Combine(Path.GetTempPath(), "galaxy-m3-checks-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(30));
var runtime = new M3GalaxyRuntime(source, backend);
if (playersOnly)
{
    var scenario = source.Scenarios.Single(s => s.Id == "tiny");
    foreach (var slots in new[] { 0, 1, (int)scenario.Empires.Maximum + 1, scenario.Stars + 1 })
    {
        var request = new GalaxyLobbyRequest("player-boundaries", "tiny", "elliptical", 1,
            Enumerable.Range(1, slots).Select(id => new GalaxyPlayerSlot(id, "economic", "pc_continental")));
        var result = source.Resolve(request);
        if (result.Success != (slots > 0 && slots <= scenario.Stars)) throw new InvalidOperationException("Неверная граница человеческих мест: " + slots);
    }
    Console.WriteLine($"PASS {PlayerCountChecks.Run(root, runtime, new("m3-real-start", "tiny", "elliptical", 1))} M3 player-count scenarios");
    return 0;
}
if (sessionOnly) { Console.WriteLine($"PASS {GalaxySessionChecks.Run(root, runtime, new("m3-real-start", "tiny", "elliptical", 1))} Galaxy/M3 session checks"); return 0; }
if (nebulaOnly) { Console.WriteLine($"PASS {GalaxySessionChecks.Run(root, runtime, new("m3-nebula-start", "tiny", "elliptical", 1), nebulaOnly: true)} nebula session smoke"); return 0; }
if (namesBudgetOnly) { Console.WriteLine($"PASS {GalaxySessionChecks.Run(root, runtime, new("m3-real-start", "huge", "elliptical", 3), namesBudgetOnly: true)} names budget smoke"); return 0; }
if (guaranteedOnly) { Console.WriteLine($"PASS {GalaxySessionChecks.Run(root, runtime, new("m3-guaranteed-start", "tiny", "elliptical", 1), guaranteedOnly: true)} guaranteed session smoke"); return 0; }
var plans = new Dictionary<(string Template, string Class, int Size), StartupBuildingPlan>();
StartupBuildingPlan Plan(string templateId, GalaxyHomeAllocation home)
{
    var key = (templateId, home.ClassId, home.Size);
    if (plans.TryGetValue(key, out var cached)) return cached;
    var template = ConstructionOrdinaryTemplate.Get(templateId);
    var facts = new Dictionary<string, bool?> {
        ["is_apocalyptic_empire"] = false, ["is_environmental_architects_empire"] = false,
        ["is_lithoid_empire"] = false, ["is_lithoid_or_infernal_empire"] = false, ["is_lithoid_devouring_swarm"] = false,
        ["has_carrier_flag:planet_unity"] = false, ["has_carrier_flag:uncapped_energy"] = false,
        ["has_carrier_flag:wilderness_planet"] = false, ["has_carrier_flag:uncapped_mining"] = false, ["has_carrier_flag:uncapped_farming"] = false };
    var context = new StartupOwnerContext(StartupAuthority.Regular, template.Ethics, template.Civics, template.OriginId,
        home.Size, planetClass: home.ClassId, countryUsesFoodGameStart: true, sourcePredicateFacts: facts);
    var plan = new StartupBuildingImporter().GenerateAsync(root, context).GetAwaiter().GetResult(); plans.Add(key, plan); return plan;
}
var players = new M3GalaxyPlayer[] { new(7, 1, "economic"), new(9, 4, "military") };
var settings = new M3GalaxySettings("m3-real-start", "tiny", "elliptical", 1);
if (navigationOnly)
{
    void NavigationCheck(bool condition, string message) { if (!condition) throw new InvalidOperationException("Navigation check: " + message); }
    static double Radius(LocalPoint point) => Math.Sqrt(point.X * point.X + point.Z * point.Z);
    static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
    var fixture=SpaceWorld.CreateFixture(7);
    var fixtureLane=fixture.Hyperlanes[0];
    NavigationCheck(fixture.Systems.All(system=>system.Radius==150) && Near(Radius(fixture.GetExitPosition(fixtureLane.FromSystemId,fixtureLane.ToSystemId)),140),"fixture 150/140 geometry changed.");
    var generated=runtime.Prepare(new("navigation-boundary-runtime","tiny","elliptical",1,GatewayFrequency:5),players,Plan);
    var world=generated.Space;
    var systemsById=world.Systems.ToDictionary(system=>system.Id,StringComparer.Ordinal);
    var permitted=world.Systems.Select(system=>system.Id).ToHashSet(StringComparer.Ordinal);
    var candidate=(from fleet in world.Fleets
        let origin=(SystemLocation)fleet.Location
        from lane in world.Hyperlanes.Where(lane=>lane.FromSystemId==origin.SystemId||lane.ToSystemId==origin.SystemId)
        let target=lane.FromSystemId==origin.SystemId?lane.ToSystemId:lane.FromSystemId
        where !Near(systemsById[origin.SystemId].Radius,systemsById[target].Radius)
        select (Fleet:fleet,From:origin.SystemId,To:target)).FirstOrDefault();
    var candidateFleet=candidate.Fleet??throw new InvalidOperationException("Navigation check: tiny source world has no home-fleet lane with different system radii.");
    var sourceRadius=systemsById[candidate.From].Radius;
    var destinationRadius=systemsById[candidate.To].Radius;
    var sourceExit=world.GetExitPosition(candidate.From,candidate.To);
    var destinationExit=world.GetExitPosition(candidate.To,candidate.From);
    NavigationCheck(Near(Radius(sourceExit),sourceRadius-SpaceWorld.EXIT_INSET)&&Near(Radius(destinationExit),destinationRadius-SpaceWorld.EXIT_INSET),"directional exits do not use each system's radius minus inset.");
    var previous=world.MoveToPoint(candidateFleet.OwnerParticipantId,candidateFleet.Id,candidate.From,
        new(((SystemLocation)candidateFleet.Location).Position.X+1,((SystemLocation)candidateFleet.Location).Position.Z),permitted);
    try { _=previous.MoveToSystem(candidateFleet.OwnerParticipantId,candidateFleet.Id,candidate.To,permitted.Where(id=>id!=candidate.To).ToHashSet(StringComparer.Ordinal)); throw new InvalidOperationException("Forbidden destination accepted."); }
    catch (SpatialRejected error) when (error.Code=="OUT_OF_SCOPE") { }
    var preserved=previous.Fleets.Single(fleet=>fleet.Id==candidate.Fleet.Id);
    NavigationCheck(preserved.Movement.Target is PointTarget&&preserved.Movement.OrderVersion==candidateFleet.Movement.OrderVersion+1,"permission failure changed the previous fleet order.");
    var moving=world.MoveToSystem(candidateFleet.OwnerParticipantId,candidateFleet.Id,candidate.To,permitted);
    NavigationCheck(moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id).Phase==FleetPhase.ApproachingExit,"move did not begin with approach.");
    for(var day=0;day<100&&moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id).Phase==FleetPhase.ApproachingExit;day++) moving=moving.AdvanceDay(moving.ElapsedDay,_=>permitted);
    var charging=moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id);
    NavigationCheck(charging.Phase==FleetPhase.Charging&&charging.Location is SystemLocation local&&Near(local.Position.X,sourceExit.X)&&Near(local.Position.Z,sourceExit.Z),"approach did not reach the source exit coordinates.");
    for(var day=0;day<20&&moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id).Phase==FleetPhase.Charging;day++) moving=moving.AdvanceDay(moving.ElapsedDay,_=>permitted);
    NavigationCheck(moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id).Phase==FleetPhase.InTransit,"charge did not transition to transit.");
    for(var day=0;day<10&&moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id).Phase==FleetPhase.InTransit;day++) moving=moving.AdvanceDay(moving.ElapsedDay,_=>permitted);
    var arrived=moving.Fleets.Single(fleet=>fleet.Id==candidateFleet.Id);
    NavigationCheck(arrived.Phase==FleetPhase.Idle&&arrived.Location is SystemLocation destination&&destination.SystemId==candidate.To&&
        Near(Radius(destination.Position),destinationRadius-SpaceWorld.EXIT_INSET)&&Near(Radius(destinationExit),destinationRadius-SpaceWorld.EXIT_INSET),"arrival did not use the destination system's exit radius.");
    Console.WriteLine($"PASS navigation different-radius lifecycle source={candidate.From}:{sourceRadius} exit={Radius(sourceExit)} destination={candidate.To}:{destinationRadius} arrival={Radius(((SystemLocation)arrived.Location).Position)} fixture=150/140");
    return 0;
}
if (habitableOnly)
{
    void HabitableCheck(bool condition, string message) { if (!condition) throw new InvalidOperationException("Habitable check: " + message); }
    var scenario = source.Scenarios.Single(item => item.Id == "tiny");
    var defaults = runtime.DefaultSettings;
    HabitableCheck(defaults.HabitableMultiplier == 1, "M3 default must be one.");
    HabitableCheck(runtime.ResolveSettings(defaults with { HabitableMultiplier = 0 }).HabitableMultiplier == 0 &&
        runtime.ResolveSettings(defaults with { HabitableMultiplier = 5 }).HabitableMultiplier == 5,
        "M3 did not forward boundary multipliers.");
    foreach (var invalid in new[] { -0.00001m, 5.00001m, 1.000001m })
    {
        try { _ = runtime.ResolveSettings(defaults with { HabitableMultiplier = invalid }); throw new InvalidOperationException("Invalid M3 habitable multiplier accepted: " + invalid); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("Множитель пригодных миров M3", StringComparison.Ordinal)) { }
    }
    var compiled = GalaxyInitializerExecution.Compile(source.Content, "exiled_system_2");
    var randomResults = new Dictionary<decimal, GalaxyInitializerResult>();
    foreach (var factor in new[] { 0m, 1m, 5m })
        randomResults.Add(factor, GalaxyInitializerExecution.Execute(source.Content, compiled,
            new GalaxyInitializerContext(GalaxyInitializerDefaults.VanillaDescriptor("sc_g", (_, _) => new(1, 1), 0, 0),
                "pc_continental", "pc_continental", factor, _ => true, (_, _) => true), NativeRandom.FromSpecifiedSeed(1)));
    HabitableCheck(randomResults[1].Draws.Any(draw => draw.Purpose == "planet-class:random_colonizable") &&
        randomResults[5].Draws.Any(draw => draw.Purpose == "planet-class:random_colonizable"), "Source random_colonizable selection was not exercised at factors 1 and 5.");
    HabitableCheck(!randomResults[0].Draws.Any(draw => draw.Purpose == "planet-class:random_colonizable"), "Zero random colonizable weight still consumed a class-selection draw.");
    var star = source.Content.Lookup(GalaxyRuntimeFamily.StarClass, "sc_b");
    HabitableCheck(star.Status == GalaxyLookupStatus.Unique &&
        star.Candidates[0].Syntax.Value.Entries.Any(entry => entry.Key == "pc_continental" &&
            entry.Value.Entries.Any(field => field.Key == "spawn_odds" && new GalaxyNumericValues(source.Content).Evaluate(field.Value) == 0.4m)),
        "Source star-class spawn_odds coefficient was not preserved.");
    HabitableCheck(randomResults[1].Bodies.Any(body => body.ClassId.StartsWith("pc_", StringComparison.Ordinal)) &&
        randomResults[5].Bodies.Any(body => body.ClassId.StartsWith("pc_", StringComparison.Ordinal)),
        "Positive source random class weight did not produce a class at multipliers one and five.");
    var generatedZero = runtime.Prepare(defaults with { Seed = "habitable-zero-runtime", HabitableMultiplier = 0, GuaranteedColonies = 1 }, players, Plan);
    HabitableCheck(generatedZero.Generated.Homes.Count == players.Length && generatedZero.Generated.Homes.All(home => home.ClassId == "pc_continental") &&
        players.All(player => generatedZero.Generated.GuaranteedAllocations.Any(allocation => allocation.ParticipantId == player.ParticipantId && allocation.GuaranteedBodyId is not null)) &&
        generatedZero.Generated.GuaranteedAllocations.Where(allocation => allocation.GuaranteedBodyId is not null).All(allocation =>
            generatedZero.Generated.Bodies.Single(body => body.Id == allocation.GuaranteedBodyId).ClassId == "pc_continental"),
        "Explicit homes or guaranteed world disappeared with random colonizable weight zero.");
    HabitableCheck(scenario.Stars == 200, "tiny source star count drifted.");
    var sessionChecks = GalaxySessionChecks.Run(root, runtime, defaults with { HabitableMultiplier = 1 }, habitableOnly: true);
    HabitableCheck(sessionChecks == 1, "Focused host/peer settings flow did not run.");
    Console.WriteLine($"PASS habitable source tiny stars={scenario.Stars} homeClasses={string.Join(',', generatedZero.Generated.Homes.Select(home => home.ClassId))} guaranteed={generatedZero.Generated.GuaranteedAllocations.Count} initializer=exiled_system_2 factors=0,1,5 sessionChecks={sessionChecks}");
    return 0;
}
if (wormholeOnly)
{
    var sessionChecks=GalaxySessionChecks.Run(root,runtime,runtime.DefaultSettings,wormholeOnly:true);
    if(sessionChecks!=1) throw new InvalidOperationException("Focused host/peer wormhole settings check did not run.");
    Console.WriteLine($"PASS wormhole host/peer settings boundaries sessionChecks={sessionChecks}");
    return 0;
}
if (gatewayOnly)
{
    void GatewayCheck(bool condition, string message) { if (!condition) throw new InvalidOperationException("Gateway check: " + message); }
    var tiny=source.Scenarios.Single(s=>s.Id=="tiny");
    var gatewayScenario=runtime.Project("unprepared",runtime.DefaultSettings,false).Scenarios.Single(s=>s.Id=="tiny");
    GatewayCheck(gatewayScenario.GatewayFrequency==tiny.Gateways,"lobby projection omitted source gateway frequency range/default.");
    foreach(var (factor,valid) in new[]{(0m,true),(1m,true),(5m,true),(5.00001m,false),(-0.00001m,false)})
    {
        var result=source.Resolve(new GalaxyLobbyRequest("gateway-bounds", "tiny", "elliptical", 1,
            players.Select(p=>new GalaxyPlayerSlot(p.ParticipantId,p.TemplateId,"pc_continental")),specialContent:new GalaxySpecialContent(Gateways:factor)));
        GatewayCheck(result.Success==valid,$"frequency bound {factor}.");
    }
    var generated=runtime.Prepare(new("gateway-tiny-runtime","tiny","elliptical",1,GatewayFrequency:5),players,Plan);
    GatewayCheck(generated.ObjectIds.SequenceEqual(generated.ObjectIds.Order(StringComparer.Ordinal)),"object IDs are not canonical.");
    GatewayCheck(generated.Generated.Gateways.Count>0,"tiny runtime did not produce a source-admitted gateway.");
    GatewayCheck(generated.ObjectIds.SequenceEqual(generated.Generated.Gateways.Select(g=>g.Type).Distinct().Order(StringComparer.Ordinal)),"M3 object requirements do not match generated object types.");
    var systems=generated.Space.Systems.Select(s=>s.Id).ToHashSet(StringComparer.Ordinal);
    var bodies=generated.Space.Bodies.Select(b=>b.Id).ToHashSet(StringComparer.Ordinal);
    var economies=players.ToDictionary(p=>p.ParticipantId,p=>p.EconomyPlanetId);
    var detailed=M3GalaxyProjection.Project(generated,generated.Generated.Gateways[0].SystemId,players[0].ParticipantId,false,systems,bodies,economies);
    var hidden=M3GalaxyProjection.Project(generated,null,players[0].ParticipantId,false,systems,bodies,economies);
    GatewayCheck(detailed.Gateways?.Any(g=>g.Id==generated.Generated.Gateways[0].Id)==true,"detailed projection omitted gateway.");
    GatewayCheck(hidden.Gateways is null||hidden.Gateways.Count==0,"hidden projection leaked gateway details.");
    GatewayCheck(detailed.Gateways!.All(g=>g.Status=="Неактивны"&&g.VisualId=="object.gateway_ruined"),"gateway binding/status drift.");
    var requirements=new Stellaris.Core.Assets.GeneratedWorldVisualRequirements(generated.ClassIds,generated.ObjectIds);
    GatewayCheck(requirements.ObjectIds?.SequenceEqual(generated.ObjectIds)==true,"generated requirements/manifest object IDs mismatch.");
    Console.WriteLine($"PASS gateway tiny runtime systems={generated.Generated.Systems.Count} gateways={generated.Generated.Gateways.Count} objects={string.Join(',',generated.ObjectIds)} hidden=0 detailed={detailed.Gateways?.Count??0}");
    return 0;
}
var count = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Test(string name, Action action) { action(); count++; Console.WriteLine("PASS " + name); }
M3GalaxyStart? sample = null;
if (miscOnly)
{
    sample=runtime.Prepare(settings,players,Plan);
    Test("ноль-весовой unlimited кандидат с нулевым шансом отклоняется до fallback draw", () =>
    {
        try { GalaxyMiscAdmissionPolicy.EnsureProgress("fixture_zero", 0, null, 0, new("fixture", 1, 1)); }
        catch (GalaxyStructureException error) when (error.Code == "misc-admission-no-progress") { return; }
        throw new Exception("Нулевой unlimited кандидат может зациклить admission при zero-total fallback.");
    });
    var candidates = new[] { new GalaxyMiscAdmissionCandidate("finite", 0, 1, 0) };
    var used = new Dictionary<string, int>(); var attempts = new Dictionary<string, int>(); var draws = 0;
    Test("selector fallback и finite failure учитывают два draw и один use", () =>
    {
        var result=GalaxyMiscAdmissionPolicy.TryAttempt(candidates, used, attempts, () => { draws++; return 0; });
        Check(!result.Spawned && result.Draws==2 && draws==2 && used["finite"]==1 && attempts["finite"]==1,"Нарушена стоимость admission draw/use.");
    });
    Test("исчерпанный finite pool возвращает empty", () =>
    {
        try { GalaxyMiscAdmissionPolicy.SelectIndex(candidates, used, 0); }
        catch (InvalidOperationException error) when (error.Message == "misc-admission-empty") { return; }
        throw new Exception("Finite кандидат после failure не исчерпан.");
    });
    Test("positive weight boundaries исключают нулевой вес, success100 делает два draw и один use", () =>
    {
        var boundaryCandidates=new[]{new GalaxyMiscAdmissionCandidate("negative",-1,1,100),new GalaxyMiscAdmissionCandidate("zero",0,1,100),new GalaxyMiscAdmissionCandidate("positive",2,1,100)};
        Check(GalaxyMiscAdmissionPolicy.SelectIndex(boundaryCandidates,new Dictionary<string,int>(),0)==2&&GalaxyMiscAdmissionPolicy.SelectIndex(boundaryCandidates,new Dictionary<string,int>(),1)==2,"Нулевой вес выбран при положительной сумме.");
        var zeroTotalCandidates=new[]{new GalaxyMiscAdmissionCandidate("negative",-1,1,100),new GalaxyMiscAdmissionCandidate("zero",0,1,100)};
        Check(GalaxyMiscAdmissionPolicy.SelectIndex(zeroTotalCandidates,new Dictionary<string,int>(),0)==1,"При zero-total fallback кандидат с отрицательным весом не был исключён.");
        var successCandidates=new[]{new GalaxyMiscAdmissionCandidate("success",3,1,100)};var successUsed=new Dictionary<string,int>();var successAttempts=new Dictionary<string,int>();var successDraws=0;
        var result=GalaxyMiscAdmissionPolicy.TryAttempt(successCandidates,successUsed,successAttempts,()=>{successDraws++;return 0;});
        Check(result.Spawned&&result.Draws==2&&successDraws==2&&successUsed["success"]==1&&successAttempts["success"]==1,"Success100 нарушил draws/use.");
    });
    Test("effective max scaling, overflow и неизвестная cluster grammar", () =>
    {
        Check(NativeGenerationRules.EffectiveInstanceLimit(3,150000)==5,"max_instances должен округляться half away from zero.");
        Check(NativeGenerationRules.EvaluateSpawnChance(100,10,60,50000)==3,"scaled_spawn_chance должен масштабироваться по числу звёзд и fixed5 factor.");
        try { NativeGenerationRules.EffectiveInstanceLimit(int.MaxValue,long.MaxValue); }
        catch (OverflowException) { goto overflowChecked; }
        throw new Exception("max_instances scaling overflow принят.");
        overflowChecked:
        var malformed=GalaxyScriptReader.Parse("modifier = { OR = { unknown_cluster = yes } factor = 0 }","fixture").Entries.Single().Value;
        try { GalaxyMiscAdmissionPolicy.ClusterGate(malformed,true); }
        catch (GalaxyStructureException error) when(error.Code=="misc-condition") { return; }
        throw new Exception("Неизвестное cluster condition принято.");
    });
    Test("genuine Trappist weight меняется от habitable scale и блокируется empire cluster", () =>
    {
        var requestHalf=source.Resolve(new GalaxyLobbyRequest("misc-weight-half","tiny","elliptical",1,players.Select(player=>new GalaxyPlayerSlot(player.ParticipantId,player.TemplateId,"pc_continental")),.5m)).Request!;
        var requestFull=source.Resolve(new GalaxyLobbyRequest("misc-weight-full","tiny","elliptical",1,players.Select(player=>new GalaxyPlayerSlot(player.ParticipantId,player.TemplateId,"pc_continental")),1m)).Request!;
        var half=new GalaxyMiscAdmission(source.Content,requestHalf).EvaluateWeight("trappist_initializer",false);
        var full=new GalaxyMiscAdmission(source.Content,requestFull).EvaluateWeight("trappist_initializer",false);
        Check(half>0&&checked(half*2)==full,"Trappist weight не масштабирован habitable_worlds_scale.");
        Check(new GalaxyMiscAdmission(source.Content,requestHalf).EvaluateWeight("trappist_initializer",true)==0,"Empire-cluster должен обнулить Trappist weight.");
    });
    Test("genuine gateway initializer validates neighbor and distance source modifiers", () =>
    {
        var request=source.Resolve(new GalaxyLobbyRequest("gateway-source-weight","tiny","elliptical",1,
            players.Select(player=>new GalaxyPlayerSlot(player.ParticipantId,player.TemplateId,"pc_continental")),
            specialContent:new GalaxySpecialContent(Gateways:1))).Request!;
        var admission=new GalaxyMiscAdmission(source.Content,request);
        Check(admission.EvaluateWeight("abandoned_gateways_01",false)==16L*GalaxyMiscAdmission.FixedScale,
            "Настоящая gateway usage modifier форма не прошла или исказила source weight.");
    });
    Test("pc_broken без source chance получает нулевой ring chance и один draw", () =>
    {
        var syntax=GalaxyScriptReader.Parse("body = { }", "fixture").Entries.Single().Value;
        var body=new GalaxyLocalBody(0,null,GalaxyBodyKind.Planet,"pc_broken",0,0,20,false,false,null,[],syntax);
        var ringDraws=0;
        Check(!GalaxyRings.Resolve(source.Content,new GalaxyNumericValues(source.Content),body,()=>{ringDraws++;return 0;})&&ringDraws==1,"pc_broken ring policy must be chance0 with one draw.");
    });
    Test("genuine Trappist initializer исполняется и два modifier эффекта разобраны", () =>
    {
        var compiled=GalaxyInitializerExecution.Compile(source.Content,"trappist_initializer");
        var defaults=GalaxyInitializerDefaults.VanillaDescriptor("rl_standard_stars",(_,_)=>throw new GalaxyStructureException("fixture-size","Unexpected missing size.",new("fixture",1,1)),0,0);
        var context=new GalaxyInitializerContext(defaults,"pc_continental","pc_continental",.5m,source.Potential,(_,_)=>true);
        var result=GalaxyInitializerExecution.Execute(source.Content,compiled,context,NativeRandom.FromSpecifiedSeed(17));
        Check(result.Bodies.Count==8,"Genuine Trappist source body count changed.");
        var modifiers=result.Bodies.Select(body=>GalaxyWorldGenerator.MiscBodyEffects("trappist_initializer",body).Modifiers).SelectMany(items=>items).ToArray();
        Check(modifiers.Length==2&&modifiers.All(modifier=>modifier.Id=="terraforming_candidate"&&modifier.Days==-1),"Trappist immutable modifier metadata is incomplete.");
    });
    sample=runtime.Prepare(new("m3-misc-smoke", "tiny", "elliptical", 1, GuaranteedColonies: 1), players, Plan);
    Check(sample.Generated.MiscAdmission?.PolicyId==GalaxyMiscAdmission.PolicyId,"Misc provenance отсутствует.");
    Check(sample.Generated.Homes.Count==players.Length && sample.Generated.Stages.Single(stage=>stage.Stage=="misc-admission").Count>0,"Tiny smoke потерял homes/admission stage.");
    Check(sample.Space.Systems.Count==sample.Generated.Systems.Count,"Tiny smoke изменил состав систем.");
    Test("modifier projection раскрывает metadata только для detailed system", () =>
    {
        var homeSystems=sample.Generated.Homes.Select(home=>home.SystemId).ToHashSet(StringComparer.Ordinal);
        var target=sample.Generated.Bodies.First(body=>body.Kind is GalaxyBodyKind.Planet or GalaxyBodyKind.Moon&&!homeSystems.Contains(body.SystemId));
        var semantics=sample.BodySemantics.Where(item=>item.BodyId!=target.Id).Append(new M3GalaxyBodySemantic(target.Id,target.ClassId,target.ParentId,target.Kind==GalaxyBodyKind.Moon?"moon":"planet",target.Size,[new("terraforming_candidate",-1)]));
        var synthetic=new M3GalaxyStart(sample.Generated,sample.Space,sample.Startup.ToDictionary(),sample.Visuals,semantics,sample.GatewayObjects);
        var systemIds=sample.Space.Systems.Select(system=>system.Id).ToHashSet(StringComparer.Ordinal);var bodyIds=sample.Space.Bodies.Select(body=>body.Id).ToHashSet(StringComparer.Ordinal);
        var economy=players.ToDictionary(player=>player.ParticipantId,player=>player.EconomyPlanetId);
        var detailed=M3GalaxyProjection.Project(synthetic,target.SystemId,7,false,systemIds,bodyIds,economy);
        Check(detailed.Bodies.Single(body=>body.Id==target.Id).Modifiers?.Single().ModifierId=="terraforming_candidate","Detailed system не показал modifier.");
        var hidden=M3GalaxyProjection.Project(synthetic,null,7,false,systemIds,bodyIds,economy);
        Check(hidden.Bodies.All(body=>body.Id!=target.Id||body.Modifiers is null),"Modifier попал в недетализированную system view.");
    });
    Test("generated visual source scales share continental baseline and core distance survives projection/privacy", () =>
    {
        var start=sample!;
        double EntityScale(string classId)
        {
            var definition=source.Content.Lookup(GalaxyRuntimeFamily.PlanetClass,classId).Candidates.Single();
            var field=definition.Syntax.Value.Entries.Last(entry=>entry.Key=="entity_scale");
            return (double)new GalaxyNumericValues(source.Content).Evaluate(field.Value,definition.File.Path);
        }
        var baseline=EntityScale("pc_continental");
        var semantics=start.BodySemantics.ToDictionary(body=>body.BodyId,StringComparer.Ordinal);
        foreach(var body in start.Space.Bodies.Where(body=>body.VisualId.StartsWith("world.",StringComparison.Ordinal)))
        {
            var semantic=semantics[body.Id];
            var size=semantic.PlanetSize??start.Generated.Bodies.Single(generated=>generated.Id==body.Id).Size;
            var expected=Math.Clamp(size/20d,.1,5)*EntityScale(semantic.ClassId)/baseline;
            Check(Math.Abs(body.Scale-expected)<1e-12,$"Generated source scale mismatch class={semantic.ClassId} kind={semantic.Kind} size={semantic.PlanetSize} actual={body.Scale:R} expected={expected:R} source={EntityScale(semantic.ClassId):R} baseline={baseline:R}.");
        }
        var systems=start.Space.Systems.Select(system=>system.Id).ToHashSet(StringComparer.Ordinal);
        var bodies=start.Space.Bodies.Select(body=>body.Id).ToHashSet(StringComparer.Ordinal);
        var projected=M3GalaxyProjection.Project(start,null,7,false,systems,bodies,players.ToDictionary(player=>player.ParticipantId,player=>player.EconomyPlanetId));
        Check(projected.GalaxyRadius==(double)start.Generated.Request.Plan.Radius,"Shared map radius missing from spatial protocol.");
        var sourceSystems=start.Generated.Systems.ToDictionary(system=>system.Id,StringComparer.Ordinal);
        foreach(var system in projected.Systems)
        {
            var position=sourceSystems[system.Id].Position;
            var expected=Math.Clamp(Math.Sqrt((double)position.X*position.X+(double)position.Y*position.Y)/((double)start.Generated.Request.Plan.Radius*100000d),0,1);
            var actual=Math.Clamp(Math.Sqrt(system.GalaxyPosition.X*system.GalaxyPosition.X+system.GalaxyPosition.Z*system.GalaxyPosition.Z)/projected.GalaxyRadius!.Value,0,1);
            Check(Math.Abs(actual-expected)<1e-12,"Client map coordinates/shared radius do not reproduce source background distance.");
        }
        Check(projected.Systems.Any(system=>Math.Sqrt(system.GalaxyPosition.X*system.GalaxyPosition.X+system.GalaxyPosition.Z*system.GalaxyPosition.Z)/projected.GalaxyRadius!.Value<.5)&&projected.Systems.Any(system=>Math.Sqrt(system.GalaxyPosition.X*system.GalaxyPosition.X+system.GalaxyPosition.Z*system.GalaxyPosition.Z)/projected.GalaxyRadius!.Value>=.75),"Tiny fixture misses core/mid/rim source band boundaries.");
        var one=projected.Systems[0].Id;
        var subset=M3GalaxyProjection.Project(start,null,7,false,new HashSet<string>(StringComparer.Ordinal){one},bodies,players.ToDictionary(player=>player.ParticipantId,player=>player.EconomyPlanetId));
        Check(subset.GalaxyRadius==projected.GalaxyRadius&&subset.Systems.Single().GalaxyPosition==projected.Systems.Single(system=>system.Id==one).GalaxyPosition,"Shared radius or permitted system coordinate changes with subset.");
        var json=System.Text.Json.JsonSerializer.Serialize(projected,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Check(json.Contains("\"galaxyRadius\"",StringComparison.Ordinal)&&!json.Contains("\"coreDistance\"",StringComparison.Ordinal),"Single shared map radius field missing from protocol.");
    });
    Console.WriteLine($"PASS misc admission + tiny smoke: {count+1} checks"); return 0;
}
if (projectionOnly)
{
    var economyIds=players.ToDictionary(player=>player.ParticipantId,player=>player.EconomyPlanetId);
    var density=source.Scenarios.Single(scenario=>scenario.Id=="huge").HyperlaneDensity.Maximum;
    var start=runtime.Prepare(settings with { ScenarioId="huge",Density=density },players,Plan);
    var systems=start.Space.Systems.Select(system=>system.Id).ToHashSet(StringComparer.Ordinal);
    var bodies=start.Space.Bodies.Select(body=>body.Id).ToHashSet(StringComparer.Ordinal);
    var inspected=start.Space.Systems.Where(system=>!start.Space.Colonies.Any(colony=>start.Space.Bodies.Single(body=>body.Id==colony.BodyId).SystemId==system.Id))
        .OrderByDescending(system=>start.Space.Bodies.Count(body=>body.SystemId==system.Id)).ThenBy(system=>system.Id,StringComparer.Ordinal).First().Id;
    var view=M3GalaxyProjection.Project(start,inspected,7,false,systems,bodies,economyIds);
    var bytes=System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(view,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    if(bytes>=750000) throw new InvalidOperationException($"Huge maximum-density projection uses {bytes} bytes; limit is 750000.");
    var onlyOne=new HashSet<string>(StringComparer.Ordinal){inspected};
    var subset=M3GalaxyProjection.Project(start,inspected,7,false,onlyOne,bodies,economyIds);
    if(subset.GalaxyRadius!=view.GalaxyRadius||subset.Systems.Count!=1||subset.Systems[0].GalaxyPosition!=view.Systems.Single(system=>system.Id==inspected).GalaxyPosition)
        throw new InvalidOperationException("Huge projection lost shared radius or widened permitted-system data.");
    Console.WriteLine($"PASS huge maximum-density projection: bytes={bytes} systems={view.Systems.Count} bodies={view.Bodies.Count} radius={view.GalaxyRadius}");
    return 0;
}
if (ringsOnly)
{
    Check(!GalaxyRings.Test(0, 0) && !GalaxyRings.Test(.5m, 50000) && GalaxyRings.Test(.5m, 49999) && GalaxyRings.Test(1, int.MaxValue), "Неверный strict fixed5 threshold.");
    var numeric = new GalaxyNumericValues(source.Content);
    GalaxyLocalBody Body(string syntax, string classId = "pc_gas_giant", GalaxyBodyKind kind = GalaxyBodyKind.Planet) =>
        new(0, null, kind, classId, 0, 0, 20, false, false, null, [], GalaxyScriptReader.Parse("body = { " + syntax + " }", "fixture").Entries.Single().Value);
    var draws = 0;
    uint Draw() { draws++; return 0; }
    Check(GalaxyRings.Resolve(source.Content, numeric, Body("has_ring = yes", "missing"), Draw), "Explicit yes требует class chance.");
    Check(!GalaxyRings.Resolve(source.Content, numeric, Body("has_ring = no"), Draw), "Explicit no потерян.");
    Check(!GalaxyRings.Resolve(source.Content, numeric, Body("has_ring = yes", kind: GalaxyBodyKind.Star), Draw) && draws == 0, "Explicit/star расходует draw.");
    var zeroClass=source.Content.PlanetClasses.First(c=>c.Syntax.Value.Entries.Any(e=>e.Key=="chance_of_ring"&&numeric.Evaluate(e.Value,c.File.Path)==0)).Id;
    Check(!GalaxyRings.Resolve(source.Content, numeric, Body("", zeroClass), Draw) && draws == 1, "Chance0 должен расходовать draw.");
    Check(GalaxyRings.Resolve(source.Content, numeric, Body(""), Draw) && draws == 2, "Class chance не применён.");
    foreach(var syntax in new[]{"has_ring = invalid", "has_ring = {}"})
    {
        try { GalaxyRings.Resolve(source.Content, numeric, Body(syntax), Draw); throw new Exception("Invalid explicit ring принят."); }
        catch(GalaxyStructureException e) when(e.Code == "invalid-ring-field") { }
    }
    var fixture=Path.Combine(Path.GetTempPath(),"rings-check-"+Guid.NewGuid().ToString("N"));
    try
    {
        foreach(var folder in new[]{"planet_classes","scripted_variables","star_classes","solar_system_initializers"}) Directory.CreateDirectory(Path.Combine(fixture,"common",folder));
        File.WriteAllText(Path.Combine(fixture,"launcher-settings.json"),"{\"rawVersion\":\"v4.5.2\"}");
        File.WriteAllText(Path.Combine(fixture,"common/planet_classes/a.txt"),"@local = 0.5 pc_one = { chance_of_ring = 1 } pc_local = { chance_of_ring = @local } pc_global = { chance_of_ring = @global } pc_missing = {} pc_bad = { chance_of_ring = 0.000001 } pc_low = { chance_of_ring = -0.1 } pc_high = { chance_of_ring = 1.1 } pc_duplicate = { chance_of_ring = 0 chance_of_ring = 1 }");
        File.WriteAllText(Path.Combine(fixture,"common/scripted_variables/a.txt"),"@global = 0.5");
        var synthetic=GalaxyRuntimeCatalog.Import(fixture,new([],[]));var numbers=new GalaxyNumericValues(synthetic);
        var calls=0;uint raw=49999;uint Next(){calls++;return raw;}
        Check(GalaxyRings.Resolve(synthetic,numbers,Body("","pc_local"),Next)&&calls==1,"Local chance variable не прочитана.");
        raw=50000;Check(!GalaxyRings.Resolve(synthetic,numbers,Body("","pc_global",GalaxyBodyKind.Moon),Next)&&calls==2,"Global chance/moon strict threshold.");
        raw=int.MaxValue;Check(GalaxyRings.Resolve(synthetic,numbers,Body("","pc_one"),Next)&&calls==3,"Chance1 должен расходовать draw.");
        foreach(var id in new[]{"pc_missing","pc_bad","pc_low","pc_high","pc_duplicate"})
        {
            try { GalaxyRings.Resolve(synthetic,numbers,Body("",id),Next);throw new Exception("Invalid ring chance принят: "+id); }
            catch(GalaxyStructureException e) when(e.Code=="invalid-ring-chance") { }
        }
        Check(calls==3,"Invalid chance расходует draw.");
    }
    finally
    {
        var full=Path.GetFullPath(fixture);var temp=Path.GetFullPath(Path.GetTempPath());
        if(Path.GetDirectoryName(full)!=Path.TrimEndingDirectorySeparator(temp)||!Path.GetFileName(full).StartsWith("rings-check-",StringComparison.Ordinal)) throw new Exception("Небезопасный fixture path.");
        if(Directory.Exists(full)) Directory.Delete(full,true);
    }
    sample = runtime.Prepare(settings, players, Plan);
    Check(sample.Generated.Bodies.Any(b=>b.HasRing) && sample.Generated.Bodies.Where(b=>b.Kind==GalaxyBodyKind.Star).All(b=>!b.HasRing), "Tiny ring metadata отсутствует/затронуты stars.");
    Check(sample.Generated.Deferred.All(d=>d.Phase is not ("has_ring" or "class-ring-probability")) && sample.Generated.Stages.Single(s=>s.Stage=="rings").Count>0 && sample.Generated.Policies.Any(p=>p.StartsWith("RingSuppliedSeedV1")), "Ring stage/deferred/policy потерян.");
    Check(sample.Space.Bodies.Count==sample.Generated.Bodies.Count+players.Length, "Кольцо создало gameplay body.");
    var systems=sample.Space.Systems.Select(s=>s.Id).ToHashSet(); var bodies=sample.Space.Bodies.Select(b=>b.Id).ToHashSet();
    var homes=sample.Generated.Homes.Select(h=>h.SystemId).ToHashSet();
    var inspected=sample.Generated.Bodies.First(b=>b.HasRing&&!homes.Contains(b.SystemId)).SystemId;
    var economy=players.ToDictionary(p=>p.ParticipantId,p=>p.EconomyPlanetId);
    var visible=M3GalaxyProjection.Project(sample,inspected,7,false,systems,bodies,economy);
    Check(visible.Bodies.Where(b=>b.Kind is "planet" or "moon").All(b=>b.HasRing==(sample.Generated.Bodies.Single(g=>g.Id==b.Id).HasRing?true:null)), "Wire потерял rings.");
    var hidden=M3GalaxyProjection.Project(sample,null,7,false,new HashSet<string>{inspected},bodies,economy);
    Check(hidden.Bodies.All(b=>b.HasRing is null), "Ring detail privacy нарушена.");
    var json=System.Text.Json.JsonSerializer.Serialize(hidden,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    Check(!json.Contains("hasRing"), "Null rings увеличивают wire.");
    var bindings=Stellaris.Core.Assets.GeneratedWorldVisualSourceCatalog.ResolveAsync(new(["pc_continental"]), (path,_)=>File.ReadAllBytesAsync(Path.Combine(root,path)), path=>File.Exists(Path.Combine(root,path))).GetAwaiter().GetResult();
    var ring=bindings.Single(b=>b.Kind=="decoration");
    Check(ring.EntityName=="gas_giant_rings_01_entity"&&ring.Scale==1&&ring.Dependencies.Count(p=>p.EndsWith(".dds"))==3, "C# source ring closure потерян.");
    if(Environment.GetEnvironmentVariable("RINGS_SERVER_MANIFEST") is {Length:>0} manifestPath)
    {
        var manifest=new Stellaris.Core.Assets.M3AssetManifestBuilder().BuildAsync(root,new Stellaris.Core.Assets.GeneratedWorldVisualRequirements(["pc_continental","pc_gas_giant"])).GetAwaiter().GetResult();
        File.WriteAllText(manifestPath,System.Text.Json.JsonSerializer.Serialize(manifest,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }
    Console.WriteLine($"PASS rings synthetic strict/explicit/no draw, source closure, tiny runtime/projection/privacy: bodies={sample.Generated.Bodies.Count}, rings={sample.Generated.Bodies.Count(b=>b.HasRing)}, draws={sample.Generated.Stages.Single(s=>s.Stage=="rings").Count}");
    return 0;
}
if (beltsOnly)
{
    sample = runtime.Prepare(settings, players, Plan);
    Check(source.Belts.File is { Sha256.Length: 64 }, "Belt fingerprint отсутствует.");
    Check(sample.Space.Bodies.Count == sample.Generated.Bodies.Count + 2, "Декорации создали physical bodies.");
    var belts = sample.Generated.Systems.SelectMany(s => s.Belts).ToArray();
    Check(belts.Length > 0 && belts.All(b => b.Width == 1 && b.Density == 1 && b.Id is not null) && belts.Select(b => b.Id).Distinct().Count() == belts.Length, "Source defaults/IDs потеряны.");
    Check(new[] { "rocky_asteroid_belt", "icy_asteroid_belt" }.All(type => belts.Any(b => b.Type == type)), "Tiny не покрывает rocky/icy.");
    Check(new[] { "pc_asteroid", "pc_ice_asteroid" }.All(sample.ClassIds.Contains), "Manifest requirements не включают belts.");
    var systems = sample.Space.Systems.Select(s => s.Id).ToHashSet();
    var bodies = sample.Space.Bodies.Select(b => b.Id).ToHashSet();
    var homes = sample.Generated.Homes.Select(h => h.SystemId).ToHashSet();
    var inspected = sample.Generated.Systems.First(s => s.Belts.Count > 0 && !homes.Contains(s.Id)).Id;
    var economy = players.ToDictionary(p => p.ParticipantId, p => p.EconomyPlanetId);
    var visible = M3GalaxyProjection.Project(sample, inspected, 7, false, systems, bodies, economy);
    Check(visible.Belts!.Any(b => b.SystemId == inspected) && visible.Belts!.All(b => b.SystemId == inspected || homes.Contains(b.SystemId)), "Belt detail privacy нарушена.");
    var hidden = M3GalaxyProjection.Project(sample, null, 7, false, new HashSet<string> { inspected }, new HashSet<string>(), economy);
    Check(hidden.Belts is null && !System.Text.Json.JsonSerializer.Serialize(hidden, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)).Contains("belts"), "Пустой/недоступный belt увеличил JSON.");
    var limited = M3GalaxyProjection.Project(sample, inspected, 7, false, new HashSet<string> { inspected }, new HashSet<string>(), economy);
    Check(limited.Belts!.All(b => b.SystemId == inspected && b.Radius > 0 && b.Width == 1 && b.Density == 1), "Скрытая система попала в belts.");
    source.VerifyStable();
    var fixture = Path.Combine(Path.GetTempPath(), "belts-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(fixture, "common/asteroid_belts"));
    var path = Path.Combine(fixture, GalaxyBeltSource.RelativePath);
    var location = new GalaxySourceLocation("fixture", 1, 1);
    var belt = new GalaxyBeltMetadata("test", 40, location);
    void Reject(Action action) { try { action(); } catch (GalaxyStructureException e) when (e.Code == "invalid-belt-source") { return; } throw new Exception("Invalid belt source принят."); }
    var absent = new GalaxyBeltSource(fixture); absent.VerifyStable(); Reject(() => absent.Resolve(belt, "s", 0));
    File.WriteAllText(path, "test = { width = 1.3 density = 0.8 }");
    var registry = new GalaxyBeltSource(fixture); var resolved = registry.Resolve(belt, "s", 0);
    Check(resolved.Width == 1.3m && resolved.Density == .8m && resolved.Id == "s/belt/0000" && resolved == registry.Resolve(belt, "s", 0), "Belt resolution нестабильно.");
    foreach (var text in new[] { "test = {} test = {}", "test = { width = 0 }", "test = { density = nan }", "test = { width = 1 width = 2 }", "other = {}" }) {
        File.WriteAllText(path, text); Reject(() => new GalaxyBeltSource(fixture).Resolve(belt, "s", 0));
    }
    try { registry.VerifyStable(); throw new Exception("Source mutation не обнаружена."); } catch (IOException) { }
    Console.WriteLine($"PASS belts/source/runtime/projection: tiny systems={sample.Space.Systems.Count}, bodies={sample.Generated.Bodies.Count}, belts={belts.Length}; rocky/icy, defaults, privacy, no extra bodies, fingerprint/stability, source failures");
    return 0;
}
if (sizesOnly)
{
    sample = runtime.Prepare(settings, players, Plan);
    var numeric = new GalaxyNumericValues(source.Content);
    double Scale(string id) {
        var definition = source.Content.Lookup(GalaxyRuntimeFamily.PlanetClass, id).Candidates.Single();
        return (double)numeric.Evaluate(definition.Syntax.Value.Entries.Last(e => e.Key == "entity_scale").Value, definition.File.Path);
    }
    Check(Scale("pc_g_star") == 20 && Scale("pc_continental") == 11 && Scale("pc_gas_giant") == 14 && Scale("pc_asteroid") == 1.5, "Изменились source entity scales.");
    foreach (var generated in sample.Generated.Bodies) {
        var semantic = sample.BodySemantics.Single(b => b.BodyId == generated.Id);
        var home = sample.Generated.Homes.SingleOrDefault(h => h.BodyId == generated.Id);
        var size = home is null ? generated.Size : sample.Startup["colony/" + home.ParticipantId].Plan.FinalPlanetSize;
        var body = sample.Space.Bodies.Single(b => b.Id == generated.Id);
        Check(semantic.PlanetSize == (generated.Kind == GalaxyBodyKind.Star ? null : size), "Потерян exact размер.");
        var expected = Math.Clamp(size / 20d, .1, 5) * Scale(generated.ClassId) / Scale(generated.Kind == GalaxyBodyKind.Star ? "pc_g_star" : "pc_continental");
        Check(Math.Abs(body.Scale - expected) < 1e-12, "Scale применён неверно: " + generated.ClassId);
    }
    Check(new[] { "pc_continental", "pc_gas_giant", "pc_asteroid" }.All(id => sample.Generated.Bodies.Any(b => b.ClassId == id)), "Нет целевых классов в tiny sample.");
    var inspected = sample.Generated.Bodies.First(b => b.ClassId == "pc_gas_giant").SystemId;
    var allSystems = sample.Space.Systems.Select(s => s.Id).ToHashSet();
    var allBodies = sample.Space.Bodies.Select(b => b.Id).ToHashSet();
    var view = M3GalaxyProjection.Project(sample, inspected, 7, false, allSystems, allBodies, players.ToDictionary(p => p.ParticipantId, p => p.EconomyPlanetId));
    Check(view.Bodies.All(b => b.PlanetSize == sample.BodySemantics.SingleOrDefault(s => s.BodyId == b.Id)?.PlanetSize), "DTO размер изменился.");
    var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    Check(view.Bodies.Where(b => b.Kind is "star" or "station").All(b => !System.Text.Json.JsonSerializer.Serialize(b, options).Contains("planetSize")), "Null размер попал в JSON.");
    Check(view.Bodies.Any(b => b.PlanetSize > 0) && sample.Generated.Bodies.Count > view.Bodies.Count, "Размер или limited detail boundary потерян.");
    Console.WriteLine($"PASS размеры/source/runtime/projection: tiny systems={sample.Space.Systems.Count}, bodies={sample.Generated.Bodies.Count}; baseline20/11, gas14, asteroid1.5; home final checked");
    return 0;
}
Test("source generated world maps to valid M3 bodies/colonies/fleets and exact source startup", () => {
    sample = runtime.Prepare(settings, players, Plan);
    Check(sample.Space.Systems.Count == 200 && sample.Space.Bodies.Count > 2000, "Generated world replaced by fixture.");
    Check(sample.Space.Colonies.Count == 2 && sample.Space.Fleets.Count == 2, "Start anchors missing.");
    Check(sample.Visuals.Count >= 10 && sample.Visuals.All(v => v.VisualId == "world." + v.ClassId), "Class visual identity lost.");
    Check(sample.BodySemantics.Any(b => b.Kind == "moon" && b.ParentBodyId is not null), "Moon semantic hierarchy lost.");
    foreach (var colony in sample.Space.Colonies)
    {
        var home = sample.Generated.Homes.Single(h => h.ParticipantId == colony.OwnerParticipantId);
        var startup = sample.Startup[colony.Id]; var body = sample.Space.Bodies.Single(b => b.Id == colony.BodyId);
        Check(colony.BodyId == home.BodyId && colony.EconomyPlanetId == players.Single(p => p.ParticipantId == home.ParticipantId).EconomyPlanetId, "Home/economy link changed.");
        Check(startup.Homeworld.PlanetClass == home.ClassId && startup.Homeworld.PlanetSize == home.Size && startup.Homeworld.InitializerId == home.InitializerId, "Home reselected.");
        Check(body.OwnerParticipantId == colony.OwnerParticipantId && startup.Plan.Gates.Count == 0 && startup.Buildings.Count > 0 && startup.Zones.Count > 0, "Startup invalid.");
    }
    Console.WriteLine($"M3 sample systems={sample.Space.Systems.Count} bodies={sample.Space.Bodies.Count} lanes={sample.Space.Hyperlanes.Count} visuals={sample.Visuals.Count}");
});
Test("seed/size/shape/density change actual world; repeat preserves fingerprint", () => {
    var repeated = runtime.Prepare(settings, players, Plan);
    Check(sample!.Generated.WorldFingerprint == repeated.Generated.WorldFingerprint, "Replay changed world.");
    var seed = runtime.Prepare(settings with { Seed = "different" }, players, Plan);
    Check(!sample.Generated.Systems.Select(s => s.Position).SequenceEqual(seed.Generated.Systems.Select(s => s.Position)), "Seed ineffective.");
    var shape = runtime.Prepare(settings with { ShapeId = "spiral_2" }, players, Plan);
    Check(!sample.Generated.Systems.Select(s => s.Position).SequenceEqual(shape.Generated.Systems.Select(s => s.Position)), "Shape ineffective.");
    var density = runtime.Prepare(settings with { Density = 3 }, players, Plan);
    Check(sample.Generated.Systems.Select(s => s.Position).SequenceEqual(density.Generated.Systems.Select(s => s.Position)) && density.Space.Hyperlanes.Count != sample.Space.Hyperlanes.Count, "Density failed topology separation.");
    var size = runtime.Prepare(settings with { ScenarioId = "small" }, players, Plan);
    Check(size.Space.Systems.Count == 400 && size.Generated.Homes.Count == 2, "Size ineffective.");
});
Test("bounded generated projection respects current1MiB wire budget and original perspective permissions", () => {
    var economyIds = players.ToDictionary(p => p.ParticipantId, p => p.EconomyPlanetId);
    var maximumDensity = source.Scenarios.Single(s => s.Id == "huge").HyperlaneDensity.Maximum;
    foreach (var (scenario, density) in new[] { ("small", 1m), ("huge", 1m), ("huge", maximumDensity) })
    {
        var start = runtime.Prepare(settings with { ScenarioId = scenario, Density = density }, players, Plan);
        var systems = start.Space.Systems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var bodies = start.Space.Bodies.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var inspected = start.Space.Systems.Where(s => !start.Space.Colonies.Any(c => start.Space.Bodies.Single(b => b.Id == c.BodyId).SystemId == s.Id))
            .OrderByDescending(s => start.Space.Bodies.Count(b => b.SystemId == s.Id)).ThenBy(s => s.Id, StringComparer.Ordinal).First().Id;
        var view = M3GalaxyProjection.Project(start, inspected, 7, false, systems, bodies, economyIds);
        var bytes = System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(view, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Console.WriteLine($"Bounded spatial {scenario} density={density}: bytes={bytes} bodies={view.Bodies.Count} fullBodies={start.Space.Bodies.Count}");
        Check(bytes < 750000, "Spatial projection leaves insufficient room within1MiB full snapshot budget.");
        Check(view.Systems.Count == start.Space.Systems.Count && view.Bodies.Any(b => b.SystemId == inspected && b.Kind == "planet"), "Inspected details missing.");
        Check(view.Bodies.Count(b => b.Kind == "star") == start.Space.Bodies.Count(b => b.Kind == "star"), "Galaxy star proxies lost.");
        var onlyOneSystem = new HashSet<string>(StringComparer.Ordinal) { inspected };
        var hiddenView = M3GalaxyProjection.Project(start, inspected, 7, false, onlyOneSystem, bodies, economyIds);
        Check(hiddenView.Systems.Count == 1 && hiddenView.Bodies.All(b => b.SystemId == inspected) && hiddenView.Hyperlanes.Count == 0 && hiddenView.Exits!.Count == 0, "Perspective system privacy widened.");
        var onlyStars = start.Space.Bodies.Where(b => b.Kind == "star").Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var filtered = M3GalaxyProjection.Project(start, inspected, 7, false, systems, onlyStars, economyIds);
        Check(filtered.Bodies.All(b => b.Kind == "star") && filtered.Colonies.Count == 0, "Perspective body privacy widened.");
    }
});
Test("generated navigation actor traverses an actual lane using existing movement", () => {
    var space = sample!.Space; var fleet = space.Fleets[0]; var home = (SystemLocation)fleet.Location;
    var lane = space.Hyperlanes.First(l => l.FromSystemId == home.SystemId || l.ToSystemId == home.SystemId);
    var target = lane.FromSystemId == home.SystemId ? lane.ToSystemId : lane.FromSystemId;
    var permitted = space.Systems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
    space = space.MoveToSystem(fleet.OwnerParticipantId, fleet.Id, target, permitted);
    for (var step = 0; step < 1000 && space.Fleets.Single(f => f.Id == fleet.Id).Phase != FleetPhase.Idle; step++) space = space.AdvanceDay(space.ElapsedDay, _ => permitted);
    var arrived = space.Fleets.Single(f => f.Id == fleet.Id);
    Check(arrived.Phase == FleetPhase.Idle && arrived.Location is SystemLocation location && location.SystemId == target, "Generated route did not arrive.");
});
Test("invalid settings fail before startup; default seed stable and blank seed replay returned", () => {
    Check(runtime.DefaultSettings.Seed == runtime.DefaultSettings.Seed, "Default seed regenerated.");
    var auto = runtime.ResolveSettings(settings with { Seed = "" }); Check(auto.Seed.Length > 0 && runtime.ResolveSettings(auto) == auto, "Blank seed replay lost.");
    var called = false;
    try { runtime.Prepare(settings with { Density = .1m }, players, (t, h) => { called = true; return Plan(t, h); }); throw new Exception("Invalid density accepted."); }
    catch (InvalidOperationException) { Check(!called, "Invalid settings reached startup."); }
});
Test("existing source economy initializes generated homes and advances a real month", () => {
    var catalog = new PlanetEconomyImporter().ImportAsync(root).GetAwaiter().GetResult();
    var population = new StartupPopulationResourceImporter().Import(root); var traits = new StartupTraitWorkforceImporter().Import(root);
    var economy = new M3BaselineEconomyProfile(catalog, population, traits.BonusesByTemplate, new Stellaris.Core.Research.TechnologyImporter().Import(root).CategoryParents); var world = economy.CreateWorld();
    var planets = new List<EntityId>();
    foreach (var player in players)
    {
        var country = world.CreateCountry(); var planet = world.CreatePlanet(country); planets.Add(planet);
        var colony = sample!.Startup.Values.Single(s => s.OwnerParticipantId == player.ParticipantId);
        economy.InitializeColony(world, country, planet, player.TemplateId, colony.Plan);
        Check(world.GetInstalledJobCapacity(planet)!.Values.Any(v => v > 0) && world.PreviewPlanetAllocations(planet).Values.Any(v => v.Workforce > 0), "Real source workforce absent.");
    }
    world.AdvanceMonth();
    Check(world.Month == 1 && planets.All(p => world.GetPlanetReport(p).Produced.Values.Any(v => v > 0)), "Real month failed.");
});
count += GalaxySessionChecks.Run(root, runtime, settings);
count += PlayerCountChecks.Run(root, runtime, settings);
Console.WriteLine($"PASS {count} Galaxy/M3 checks");
return 0;
