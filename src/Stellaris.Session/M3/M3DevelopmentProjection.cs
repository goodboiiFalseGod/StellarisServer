using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Assets;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3;

internal static class M3DevelopmentProjection
{
    // Передаём повторяющиеся metadata один раз в пределах разрешённой проекции.
    // Индексы сохраняют порядок и кратность; live данные каталога не изменяются.
    internal static M3DevelopmentView CompactCatalog(M3DevelopmentView view)
    {
        if (view.Colonies.Count == 0) return view;
        var terms = new List<M3ConstructionSourceTermView>();
        var termIds = new Dictionary<M3ConstructionSourceTermView, int>();
        var slotSets = new List<IReadOnlyList<string>>();
        var slotSetIds = new Dictionary<IReadOnlyList<string>, int>(new SlotSetComparer());
        M3ConstructionCatalogView Compact(M3ConstructionCatalogView entry)
        {
            IReadOnlyList<int>? indices = null;
            int? slots = null;
            if (entry.SourceRecipe is { } recipe)
                indices = Array.AsReadOnly(recipe.Select(term => {
                    if (!termIds.TryGetValue(term, out var index)) { index = terms.Count; terms.Add(term); termIds.Add(term, index); }
                    return index;
                }).ToArray());
            if (entry.CompatibleSlotIds is { } compatible)
            {
                if (!slotSetIds.TryGetValue(compatible, out var index))
                {
                    index = slotSets.Count;
                    var frozen = Array.AsReadOnly(compatible.ToArray());
                    slotSets.Add(frozen); slotSetIds.Add(frozen, index);
                }
                slots = index;
            }
            return entry with { SourceRecipe = null, CompatibleSlotIds = null, SourceRecipeIndices = indices, CompatibleSlotSetIndex = slots };
        }
        var colonies = Array.AsReadOnly(view.Colonies.Select(colony => colony with {
            Catalog = Array.AsReadOnly(colony.Catalog.Select(Compact).ToArray()) }).ToArray());
        var strings = terms.SelectMany(term => new[] { term.Kind, term.TargetId, term.Expression, term.ConditionDisposition, term.ApplicationScope })
            .Where(text => text is not null).Select(text => text!).Distinct(StringComparer.Ordinal).ToArray();
        return view with { Colonies = colonies, SourceRecipeTerms = Array.AsReadOnly(terms.ToArray()), CatalogSlotSets = Array.AsReadOnly(slotSets.ToArray()),
            SourceRecipeEncoding = M3DevelopmentWireConverter.ENCODING, SourceRecipeStrings = Array.AsReadOnly(strings) };
    }

    private sealed class SlotSetComparer : IEqualityComparer<IReadOnlyList<string>>
    {
        public bool Equals(IReadOnlyList<string>? first, IReadOnlyList<string>? second) => ReferenceEquals(first, second) || first is not null && second is not null && first.SequenceEqual(second, StringComparer.Ordinal);
        public int GetHashCode(IReadOnlyList<string> value) { var hash = new HashCode(); foreach (var slot in value) hash.Add(slot, StringComparer.Ordinal); return hash.ToHashCode(); }
    }

    public static M3DevelopmentView Project(ConstructionProjection? content, M3SpatialView spatial, M3ViewContext perspective, IReadOnlyDictionary<int, ConstructionOrdinaryTemplate> templates, M3AssetManifest manifest, IReadOnlyDictionary<string, M3StartupColony>? startupColonies = null, IReadOnlyDictionary<string, M3DevelopmentEconomyView>? economies = null, string? sourceEconomyProfileId = null, IReadOnlyDictionary<string, long>? colonyRevisions = null, IReadOnlyDictionary<string, M3DevelopmentColonyView>? constructionViews = null)
    {
        var imageKey = ResolveImageKey(content, manifest);
        var gates = Array.AsReadOnly(new[] { new M3DevelopmentGate("dependency-unresolved", content is null ? "construction-profile-unavailable" : "construction-profile-closure") }.Concat(imageKey is null ? new[] { new M3DevelopmentGate("dependency-unresolved", "construction-image-binding") } : Array.Empty<M3DevelopmentGate>()).ToArray());
        var liveReady = manifest.SchemaVersion == 1 && !string.IsNullOrWhiteSpace(manifest.ProfileId) && !string.IsNullOrWhiteSpace(manifest.ProfileDigest)
            && startupColonies is { Count: > 0 } && economies is not null && constructionViews is not null
            && startupColonies.Count == constructionViews.Count && startupColonies.All(pair => pair.Value.Plan.Gates.Count == 0
                && economies.ContainsKey(pair.Key) && constructionViews.TryGetValue(pair.Key, out var view) && view.ColonyId == pair.Key);
        IReadOnlyList<M3DevelopmentGate> projectionGates = liveReady ? Array.Empty<M3DevelopmentGate>() : gates;
        var colonies = perspective.Mode != "player" ? Array.Empty<M3DevelopmentColonyView>() : spatial.Colonies.Where(colony => colony.OwnerParticipantId == perspective.ParticipantId).Select(colony =>
        {
            templates.TryGetValue(colony.OwnerParticipantId, out var template);
            var context = template is null ? new ConstructionContext() : template.CreateInitialConstructionContext(new(true, new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Ai] = false }));
            var assessment = content is null ? null : ConstructionAssessor.Assess(content, context);
            var colonyGates = Array.AsReadOnly((assessment?.Gates.Select(gate => new M3DevelopmentGate(gate.Code, gate.DependencyId)) ?? projectionGates).Concat(projectionGates).Concat(template is null ? new[] { new M3DevelopmentGate("dependency-unresolved", "empire-template-selection") } : Array.Empty<M3DevelopmentGate>()).Distinct().ToArray());
            var catalog = content is null || assessment is null ? Array.Empty<M3ConstructionCatalogView>() : new[] { new M3ConstructionCatalogView(content.Definition.Id, "building", imageKey, "unresolved", Array.Empty<string>(), null, colonyGates) };
            M3StartupColony? startup = null;
            startupColonies?.TryGetValue(colony.Id, out startup);
            var startupSlots = startup is null ? Array.Empty<M3DevelopmentSlotView>() : startup.Slots.Select(slot => new M3DevelopmentSlotView(slot.InstanceId, slot.SlotType, null, null, Array.Empty<M3DevelopmentGate>())).ToArray();
            var startupBuildings = startup is null ? Array.Empty<M3StartupBuildingView>() : startup.Buildings.Select(item => new M3StartupBuildingView(item.InstanceId, item.DefinitionId, item.SlotInstanceId, item.ZoneId, item.Order, ResolveStartupImageKey(item.DefinitionId, manifest), item.CompatibleZoneInstanceIds,
                PlanetLocalizationBindings.Get("building", item.DefinitionId).NameKey, PlanetLocalizationBindings.Get("building", item.DefinitionId).DescriptionKey)).ToArray();
            var startupDistricts = startup is null ? Array.Empty<M3StartupDistrictView>() : startup.Districts.Select(item => new M3StartupDistrictView(item.InstanceId, item.DefinitionId, item.Level,
                PlanetLocalizationBindings.Get("district", item.DefinitionId).NameKey, PlanetLocalizationBindings.Get("district", item.DefinitionId).DescriptionKey)).ToArray();
            var startupZones = startup is null ? Array.Empty<M3StartupZoneView>() : startup.Zones.Select(item => new M3StartupZoneView(item.InstanceId, item.DistrictInstanceId, item.SlotInstanceId, item.ZoneId, item.SlotNumber,
                PlanetLocalizationBindings.Get("zone", item.ZoneId).NameKey, PlanetLocalizationBindings.Get("zone", item.ZoneId).DescriptionKey)).ToArray();
            var startupFeatures = startup is null ? Array.Empty<M3StartupFeatureView>() : startup.Features.Select(item => new M3StartupFeatureView(item.InstanceId, item.DefinitionId, item.IsBlocker, item.IsHidden, item.HiddenByBlockerInstanceId, item.HiddenFeatureInstanceId, item.Order,
                PlanetLocalizationBindings.Get("feature", item.DefinitionId).NameKey, PlanetLocalizationBindings.Get("feature", item.DefinitionId).DescriptionKey)).ToArray();
            var startupDistrictCaps = startup is null ? Array.Empty<M3StartupDistrictCapView>() : startup.Plan.DistrictCaps.Select(item => new M3StartupDistrictCapView(item.ModifierId, item.Value)).ToArray();
            var startupGates = startup is null ? Array.Empty<M3DevelopmentGate>() : startup.Plan.Gates.Select(gate => new M3DevelopmentGate("dependency-unresolved", gate.Detail))
                .Concat(economies?.ContainsKey(colony.Id) == true ? Array.Empty<M3DevelopmentGate>() : [new M3DevelopmentGate("dependency-unresolved", "startup-jobs-and-economics")]).ToArray();
            M3DevelopmentEconomyView? economy = null;
            if (economies is not null) { economies.TryGetValue(colony.Id, out economy); }
            M3DevelopmentColonyView? liveConstruction = null;
            constructionViews?.TryGetValue(colony.Id, out liveConstruction);
            var revision = colonyRevisions?.GetValueOrDefault(colony.Id) ?? 0;
            // Revisions are supplied only for owner-visible economy snapshots.
            var projected = new M3DevelopmentColonyView(colony.Id, colony.EconomyPlanetId, colony.OwnerParticipantId, liveConstruction?.Revision ?? revision, template is null ? "unresolved" : "individualist", false, null,
                spatial.Bodies.FirstOrDefault(body => body.Id == colony.BodyId)?.SystemId,
                Array.AsReadOnly(startupSlots), Array.AsReadOnly(catalog), Array.Empty<M3ConstructionInstalledView>(), Array.Empty<M3ConstructionOrderView>(), Array.Empty<M3DevelopmentFeatureView>(), Array.Empty<M3DevelopmentJobView>(), Array.AsReadOnly(colonyGates.Concat(startupGates).ToArray()),
                startup?.Homeworld.InitializerId, startup?.Homeworld.PlanetClass, startup?.Plan.FinalPlanetSize, Array.AsReadOnly(startupDistricts), Array.AsReadOnly(startupBuildings), startup?.Homeworld.PlanetSize,
                Array.AsReadOnly(startupZones), Array.AsReadOnly(startupFeatures), Array.AsReadOnly(startupDistrictCaps), economy);
            return liveConstruction is null ? projected : projected with
            {
                Slots = liveConstruction.Slots,
                Catalog = Array.AsReadOnly(liveConstruction.Catalog.Select(item => item with { ImageDefinitionKey = M3AssetManifestBuilder.ResolvePlanetImageKey(manifest, item.Kind, item.DefinitionId) }).ToArray()),
                Installed = Array.AsReadOnly(liveConstruction.Installed.Select(item => item with
                {
                    ImageDefinitionKey = M3AssetManifestBuilder.ResolvePlanetImageKey(manifest, item.Kind, item.DefinitionId),
                    Upgrades = item.Upgrades is null ? null : Array.AsReadOnly(item.Upgrades.Select(upgrade => upgrade with
                    { ImageDefinitionKey = M3AssetManifestBuilder.ResolvePlanetImageKey(manifest, "building", upgrade.DefinitionId) }).ToArray())
                }).ToArray()),
                Queue = liveConstruction.Queue,
                QueuePolicy = liveConstruction.QueuePolicy,
                DistrictPool = liveConstruction.DistrictPool,
                DistrictTypePools = liveConstruction.DistrictTypePools,
                Features = liveConstruction.Features,
                Gates = Array.AsReadOnly(startupGates),
                StartupDistricts = liveConstruction.StartupDistricts,
                StartupBuildings = liveConstruction.StartupBuildings,
                StartupZones = liveConstruction.StartupZones,
                StartupFeatures = liveConstruction.StartupFeatures,
                StartupDistrictCaps = liveConstruction.StartupDistrictCaps
            };
        }).ToArray();
        return new(sourceEconomyProfileId ?? content?.ProfileId ?? "construction-unavailable", Array.AsReadOnly(colonies), projectionGates);
    }

    private static string? ResolveImageKey(ConstructionProjection? content, M3AssetManifest manifest)
    {
        const string ROOT = "common/buildings/03_resource_buildings.txt";
        const string IMAGE = "gfx/interface/icons/buildings/building_hydroponics_farm.dds";
        if (content is null || content.Definition.Id != "building_hydroponics_farm" || content.Definition.Origin.Source != ROOT || manifest.SchemaVersion != 1) { return null; }
        var files = manifest.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        if (!files.ContainsKey(ROOT) || !files.ContainsKey(IMAGE) || !content.Files.Any(file => file.RelativePath == ROOT)) { return null; }
        foreach (var source in content.Files)
        {
            if (files.TryGetValue(source.RelativePath, out var asset) && (source.ByteLength != asset.ByteLength || source.Sha256 != asset.Sha256)) { return null; }
        }
        return "building.building_hydroponics_farm";
    }

    private static string? ResolveStartupImageKey(string definitionId, M3AssetManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(definitionId) || definitionId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')) { return null; }
        var iconStem = definitionId == "building_machine_assembly_plant" ? "building_robot_assembly_plant" : definitionId;
        var imagePath = "gfx/interface/icons/buildings/" + iconStem + ".dds";
        return manifest.Files.Any(file => file.RelativePath == imagePath) ? "building." + definitionId : null;
    }
}
